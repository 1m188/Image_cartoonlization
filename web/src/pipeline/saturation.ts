/**
 * 饱和度调整 —— 移植自 saturation_adjust.m
 *
 * MATLAB 语义：
 *   gray = rgb2gray(img)                          % Rec.601 权重
 *   res  = (1-S).*gray + S.*img                   % 灰度图与原图的插值/外推
 *   res  = uint8(res)                             % 四舍五入 + 截断到 [0,255]
 *
 * S > 1 为外推（更鲜艳），S < 1 为去饱和，S = 1 为原图。
 * src/dst 都是 RGBA（A 通道原样保留），按 [0,255] 整数处理。
 */
const WR = 0.2989;
const WG = 0.587;
const WB = 0.114;

export function saturationAdjust(
  src: Uint8ClampedArray,
  scalar: number,
  dst?: Uint8ClampedArray,
): Uint8ClampedArray {
  const out = dst ?? new Uint8ClampedArray(src.length);
  const t = 1 - scalar;
  for (let i = 0; i < src.length; i += 4) {
    const r = src[i];
    const g = src[i + 1];
    const b = src[i + 2];
    // 有意为之的近似：MATLAB 的 rgb2gray(uint8) 会先把灰度取整到 uint8，
    // 这里保留浮点灰度，与 MATLAB 的差异 ≤ 0.5 LSB，换取更少的取整步骤
    const gray = WR * r + WG * g + WB * b;
    out[i] = Math.round(t * gray + scalar * r);
    out[i + 1] = Math.round(t * gray + scalar * g);
    out[i + 2] = Math.round(t * gray + scalar * b);
    out[i + 3] = src[i + 3];
  }
  return out;
}
