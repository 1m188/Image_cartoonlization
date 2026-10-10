/**
 * 卡通化主管线 —— 移植自 cartoonlize.m
 *
 * 步骤（与 MATLAB 一一对应）：
 * 1. saturation_adjust(img, S)          饱和度调整
 * 2. 归一化到 [0,1]（此处隐含在类型约定中）
 * 3. bfilter2 × iterations              Lab 空间双边滤波
 * 4. rgb2gray(滤波结果) → edge(...,'sobel',thresh)  边缘掩膜
 * 5. 滤波图 × (1-mask)                  掩膜处置黑描边
 */
import { bilateralFilterRGB } from './bilateral';
import { rgb2gray01 } from './color';
import { edgeMask } from './edges';
import { saturationAdjust } from './saturation';

export interface CartoonParams {
  /** 饱和度系数，对应 saturation_scalar，默认 2 */
  saturation: number;
  /** 双边滤波窗口半径，对应 radius，默认 10 */
  radius: number;
  /** 空间 σd，对应 sigma(1)，默认 3 */
  sigmaD: number;
  /** 色彩 σr，对应 sigma(2)，默认 0.1 */
  sigmaR: number;
  /** 边缘阈值，对应 edge_thresh，默认 0.02 */
  edgeThresh: number;
  /** 滤波迭代次数，对应 loop_num，默认 1 */
  iterations: number;
}

export const DEFAULT_PARAMS: CartoonParams = {
  saturation: 2,
  radius: 10,
  sigmaD: 3,
  sigmaR: 0.1,
  edgeThresh: 0.02,
  iterations: 1,
};

/** 管线各阶段占总进度的比例（双边滤波占大头） */
export const STAGE_WEIGHTS = { saturation: 0.05, bilateral: 0.85, edges: 0.1 } as const;

/**
 * 对滤波完成的图像应用边缘掩膜（灰度 → Sobel → 置黑）。
 * 从 pipeline 中拆出来，供并行 worker 的 finish 阶段复用。
 */
export function applyEdgeOutline(
  filtered: Uint8ClampedArray,
  width: number,
  height: number,
  edgeThresh: number,
): Uint8ClampedArray {
  const gray = rgb2gray01(filtered);
  const mask = edgeMask(gray, width, height, edgeThresh);
  for (let p = 0; p < mask.length; p++) {
    if (mask[p] === 1) {
      const i = p * 4;
      filtered[i] = 0;
      filtered[i + 1] = 0;
      filtered[i + 2] = 0;
    }
  }
  return filtered;
}

export function cartoonize(
  src: Uint8ClampedArray,
  width: number,
  height: number,
  params: CartoonParams,
  onProgress?: (fraction: number, stage: string) => void,
): Uint8ClampedArray {
  const iterations = Math.max(1, Math.round(params.iterations));

  // 1. 饱和度调整
  onProgress?.(STAGE_WEIGHTS.saturation, '饱和度调整');
  const saturated = saturationAdjust(src, params.saturation);

  // 2. 双边滤波 × iterations（在归一化 Lab 空间）
  let filtered = saturated;
  for (let k = 0; k < iterations; k++) {
    const base = STAGE_WEIGHTS.saturation + (STAGE_WEIGHTS.bilateral * k) / iterations;
    filtered = bilateralFilterRGB(filtered, width, height, params, (f) => {
      onProgress?.(base + (STAGE_WEIGHTS.bilateral * f) / iterations, '双边滤波');
    });
  }

  // 3. 边缘掩膜 + 置黑
  onProgress?.(STAGE_WEIGHTS.saturation + STAGE_WEIGHTS.bilateral, '边缘检测');
  return applyEdgeOutline(filtered, width, height, params.edgeThresh);
}
