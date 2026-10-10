/**
 * 双边滤波（彩色图，CIELab 空间）—— 移植自 bfilter2.m / bfltColor.m
 *
 * MATLAB 语义（bfltColor）：
 *   A       = applycform(A, makecform('srgb2lab'))    % 先转 Lab
 *   sigma_r = 100 * sigma_r                           % 范围方差放大 100 倍（Lab 尺度）
 *   G       = exp(-(x²+y²) / (2σd²))                  % 窗口内高斯空间权重
 *   H       = exp(-(ΔL²+Δa²+Δb²) / (2σr²))            % Lab 色彩距离权重
 *   B(p)    = Σ F·I / Σ F,  F = G.*H                  % 归一化加权和
 *   边界：窗口越界部分直接截掉（等效边界复制），归一化只用窗口内实际权重
 *
 * 性能设计：
 * - 范围权重 exp 用一维 LUT（对距离平方分桶）替代逐像素 exp
 * - 按行分带（band）处理：Lab 中间结果只保留当前带 + 半径 halo，
 *   内存 O(band 高度 × 宽度) 而非 O(整图)，12MP 原图也不会爆内存
 */
import { labToRgb, rgbToLab } from './color';

export interface BilateralOptions {
  /** 窗口半径 w（窗口边长 2w+1），对应 MATLAB radius */
  radius: number;
  /** 空间高斯 σd，对应 MATLAB sigma(1) */
  sigmaD: number;
  /** 色彩高斯 σr，对应 MATLAB sigma(2)（内部 ×100 转到 Lab 尺度） */
  sigmaR: number;
}

/**
 * LUT 桶数：2^18 桶覆盖最大可能距离平方（140050），桶宽 ≈0.53。
 * 量化相对误差 ≈ 桶宽/(2σrLab²)：滑条下限 σr=0.03（σrLab=3）时 ≤ ~3%，
 * 默认 σr=0.1（σrLab=10）时 ≤ ~0.3%，均无视觉影响。
 */
const LUT_BITS = 18;
const LUT_SIZE = 1 << LUT_BITS;

/** Lab 分量差的最大可能距离平方：|ΔL|≤100, |Δa|,|Δb|≤255 */
const MAX_D2 = 100 * 100 + 255 * 255 + 255 * 255;

/** 单带 Lab 缓冲的目标字节数（约 8MB），控制峰值内存 */
const BAND_TARGET_BYTES = 8 << 20;

export function bilateralFilterRGB(
  src: Uint8ClampedArray,
  width: number,
  height: number,
  options: BilateralOptions,
  onProgress?: (fraction: number) => void,
  dst?: Uint8ClampedArray,
): Uint8ClampedArray {
  if (width <= 0 || height <= 0) throw new Error('图像尺寸必须为正');
  if (src.length < width * height * 4) throw new Error('输入缓冲小于图像尺寸');

  const w = Math.max(1, Math.ceil(options.radius));
  const sigmaD = options.sigmaD;
  const sigmaRLab = options.sigmaR * 100; // 与 bfltColor.m 的 sigma_r = 100*sigma_r 一致
  if (sigmaD <= 0 || sigmaRLab <= 0) throw new Error('sigmaD/sigmaR 必须为正');

  const win = 2 * w + 1;

  // 预计算高斯空间权重 G（与 bfltColor.m 的 meshgrid+exp 相同）
  const domain = new Float32Array(win * win);
  for (let dy = -w; dy <= w; dy++) {
    for (let dx = -w; dx <= w; dx++) {
      domain[(dy + w) * win + (dx + w)] = Math.exp(
        -(dx * dx + dy * dy) / (2 * sigmaD * sigmaD),
      );
    }
  }

  // 范围权重 LUT：lut[i] = exp(-d²/(2σr²))，d² = i * MAX_D2/(LUT_SIZE-1)
  const lut = new Float32Array(LUT_SIZE);
  const d2Step = MAX_D2 / (LUT_SIZE - 1);
  const twoSigmaR2 = 2 * sigmaRLab * sigmaRLab;
  for (let i = 0; i < LUT_SIZE; i++) lut[i] = Math.exp(-(i * d2Step) / twoSigmaR2);
  const lutScale = (LUT_SIZE - 1) / MAX_D2;

  const out = dst ?? new Uint8ClampedArray(src.length);

  // 分带：保证每带 ≥ 窗口高度，且 Lab 缓冲不超过目标内存
  const bandBytesPerRow = width * 3 * 4;
  let bandRows = Math.max(win, Math.floor(BAND_TARGET_BYTES / bandBytesPerRow));
  if (bandRows > height) bandRows = height;

  // 复用的带缓冲
  const maxBandRows = Math.min(height, bandRows + 2 * w); // 含上下 halo
  const bandLab = new Float32Array(maxBandRows * width * 3);
  const bandLabOut = new Float32Array(maxBandRows * width * 3);
  const bandRGB = new Uint8ClampedArray(maxBandRows * width * 4);

  for (let y0 = 0; y0 < height; y0 += bandRows) {
    const y1 = Math.min(height, y0 + bandRows);
    const ry0 = Math.max(0, y0 - w);
    const ry1 = Math.min(height, y1 + w);
    const bandPixels = (ry1 - ry0) * width;

    // 当前带（含 halo）的 Lab
    const bandSrc = src.subarray(ry0 * width * 4, ry1 * width * 4);
    rgbToLab(bandSrc, bandPixels, bandLab.subarray(0, bandPixels * 3));

    for (let y = y0; y < y1; y++) {
      const tyMin = Math.max(0, y - w);
      const tyMax = Math.min(height - 1, y + w);
      const rowBase = (y - ry0) * width * 3;

      for (let x = 0; x < width; x++) {
        const txMin = Math.max(0, x - w);
        const txMax = Math.min(width - 1, x + w);

        const cL = bandLab[rowBase + x * 3];
        const ca = bandLab[rowBase + x * 3 + 1];
        const cb = bandLab[rowBase + x * 3 + 2];

        let sumW = 0;
        let sumL = 0;
        let sumA = 0;
        let sumB = 0;

        for (let ty = tyMin; ty <= tyMax; ty++) {
          const tRow = (ty - ry0) * width * 3;
          const domRow = (ty - (y - w)) * win;
          for (let tx = txMin; tx <= txMax; tx++) {
            const tOff = tRow + tx * 3;
            const dLd = bandLab[tOff] - cL;
            const dad = bandLab[tOff + 1] - ca;
            const dbd = bandLab[tOff + 2] - cb;
            const d2 = dLd * dLd + dad * dad + dbd * dbd;

            let idx = (d2 * lutScale) | 0;
            if (idx > LUT_SIZE - 1) idx = LUT_SIZE - 1;

            const f = domain[domRow + (tx - (x - w))] * lut[idx];
            sumW += f;
            sumL += f * bandLab[tOff];
            sumA += f * bandLab[tOff + 1];
            sumB += f * bandLab[tOff + 2];
          }
        }

        const o = ((y - y0) * width + x) * 3;
        bandLabOut[o] = sumL / sumW;
        bandLabOut[o + 1] = sumA / sumW;
        bandLabOut[o + 2] = sumB / sumW;
      }
    }

    // 滤波后的带（Lab）→ RGB，写入输出
    const outRows = y1 - y0;
    labToRgb(
      bandLabOut.subarray(0, outRows * width * 3),
      outRows * width,
      bandRGB.subarray(0, outRows * width * 4),
    );
    for (let row = 0; row < outRows; row++) {
      out.set(
        bandRGB.subarray(row * width * 4, (row + 1) * width * 4),
        (y0 + row) * width * 4,
      );
    }

    onProgress?.(y1 / height);
  }

  return out;
}
