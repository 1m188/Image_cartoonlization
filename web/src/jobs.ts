/**
 * worker 计算内核（纯函数，供 worker.ts 与编排层测试共用，避免逻辑漂移）。
 */
import { bilateralFilterRGB } from './pipeline/bilateral';
import { applyEdgeOutline, type CartoonParams } from './pipeline/pipeline';
import { saturationAdjust } from './pipeline/saturation';

/**
 * 'strip' 消息内核：饱和度 + 双边滤波 × iterations（不含描边）。
 * onStripProgress 的 fraction 已按轮数归一到 [0,1]。
 */
export function processStrip(
  src: Uint8ClampedArray,
  width: number,
  height: number,
  params: CartoonParams,
  onStripProgress?: (fraction: number) => void,
): Uint8ClampedArray {
  const iterations = Math.max(1, Math.round(params.iterations));
  let cur = saturationAdjust(src, params.saturation);
  for (let k = 0; k < iterations; k++) {
    cur = bilateralFilterRGB(cur, width, height, params, (f) => {
      onStripProgress?.((k + f) / iterations);
    });
  }
  return cur;
}

/** 'finish' 消息内核：灰度 → Sobel 相对阈值 → 掩膜置黑 */
export function processFinish(
  filtered: Uint8ClampedArray,
  width: number,
  height: number,
  edgeThresh: number,
): Uint8ClampedArray {
  return applyEdgeOutline(filtered, width, height, edgeThresh);
}
