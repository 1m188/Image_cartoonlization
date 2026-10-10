import { describe, expect, it } from 'vitest';
import { DEFAULT_PARAMS, cartoonize } from '../src/pipeline/pipeline';

/**
 * 合成测试图：平坦背景 + 一个暗色方块（会产生清晰轮廓）。
 * 背景必须平坦：相对阈值语义下（thresh × 全图最大梯度），
 * 明显的背景渐变本身会被判为边缘。
 */
function syntheticScene(w: number, h: number): Uint8ClampedArray {
  const buf = new Uint8ClampedArray(w * h * 4);
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const i = (y * w + x) * 4;
      const inSquare = x >= 6 && x < 18 && y >= 6 && y < 18;
      const v = inSquare ? 30 : 128;
      buf[i] = v;
      buf[i + 1] = v;
      buf[i + 2] = v;
      buf[i + 3] = 255;
    }
  }
  return buf;
}

const FAST_PARAMS = { ...DEFAULT_PARAMS, radius: 4, sigmaD: 2, sigmaR: 0.1 };

describe('cartoonize 主管线（对应 cartoonlize.m）', () => {
  it('输出尺寸与输入一致', () => {
    const w = 24;
    const h = 24;
    const src = syntheticScene(w, h);
    const out = cartoonize(src, w, h, FAST_PARAMS);
    expect(out.length).toBe(src.length);
    expect(out).toBeInstanceOf(Uint8ClampedArray);
  });

  it('默认参数下合成场景能产生黑色描边（边缘掩膜生效）', () => {
    const w = 24;
    const h = 24;
    const src = syntheticScene(w, h);
    const out = cartoonize(src, w, h, FAST_PARAMS);
    let blackPixels = 0;
    for (let i = 0; i < out.length; i += 4) {
      if (out[i] === 0 && out[i + 1] === 0 && out[i + 2] === 0) blackPixels++;
    }
    expect(blackPixels).toBeGreaterThan(0);
  });

  it('非边缘平坦区域保持接近原亮度（卡通化不破坏内容）', () => {
    const w = 24;
    const h = 24;
    const src = syntheticScene(w, h);
    const out = cartoonize(src, w, h, FAST_PARAMS);
    // 远离方块的左上角区域，且边缘阈值不至于把渐变判成边缘
    const i = (2 * w + 2) * 4;
    expect(Math.abs(out[i] - src[i])).toBeLessThanOrEqual(30);
  });

  it('iterations=2（多轮滤波）不报错且结果确定', () => {
    const w = 24;
    const h = 24;
    const src = syntheticScene(w, h);
    const p = { ...FAST_PARAMS, iterations: 2 };
    const a = cartoonize(src, w, h, p);
    const b = cartoonize(src, w, h, p);
    expect(Array.from(a)).toEqual(Array.from(b));
  });

  it('同样的输入与参数，输出确定一致', () => {
    const w = 24;
    const h = 24;
    const src = syntheticScene(w, h);
    const a = cartoonize(src, w, h, FAST_PARAMS);
    const b = cartoonize(src, w, h, FAST_PARAMS);
    expect(Array.from(a)).toEqual(Array.from(b));
  });

  it('1×N 单行图像也能跑通管线', () => {
    const w = 40;
    const h = 1;
    const src = syntheticScene(w, 8).subarray(0, w * 4); // 取一行像素
    const out = cartoonize(src, w, h, { ...FAST_PARAMS, radius: 3 });
    expect(out.length).toBe(src.length);
  });

  it('alpha 通道全程保留为 255', () => {
    const w = 24;
    const h = 24;
    const src = syntheticScene(w, h);
    const out = cartoonize(src, w, h, FAST_PARAMS);
    for (let i = 3; i < out.length; i += 4) expect(out[i]).toBe(255);
  });
});
