import { describe, expect, it } from 'vitest';
import { saturationAdjust } from '../src/pipeline/saturation';

/** 生成纯色 RGBA 图像 */
function solid(w: number, h: number, r: number, g: number, b: number): Uint8ClampedArray {
  const buf = new Uint8ClampedArray(w * h * 4);
  for (let i = 0; i < w * h; i++) {
    buf[i * 4] = r;
    buf[i * 4 + 1] = g;
    buf[i * 4 + 2] = b;
    buf[i * 4 + 3] = 255;
  }
  return buf;
}

describe('saturationAdjust（对应 saturation_adjust.m）', () => {
  it('S=1 时输出与输入一致（插值退化）', () => {
    const src = solid(4, 4, 200, 100, 50);
    const out = saturationAdjust(src, 1);
    expect(Array.from(out)).toEqual(Array.from(src));
  });

  it('S=1.2 时按 (1-S)·gray + S·img 外推，且不截断的通道逐像素正确', () => {
    // (200,100,50): gray = 0.2989·200 + 0.587·100 + 0.114·50 = 124.18
    // S=1.2 → (215.164, 95.164, 35.164) → 四舍五入 (215, 95, 35)
    const src = solid(2, 2, 200, 100, 50);
    const out = saturationAdjust(src, 1.2);
    expect(out[0]).toBe(215);
    expect(out[1]).toBe(95);
    expect(out[2]).toBe(35);
  });

  it('S=2 时外推超出 [0,255] 的通道按 uint8 语义截断', () => {
    // (200,100,50), S=2: R=-124.18+400=275.82→255, G=-124.18+200=75.82→76, B=-124.18+100=-24.18→0
    const src = solid(2, 2, 200, 100, 50);
    const out = saturationAdjust(src, 2);
    expect(out[0]).toBe(255);
    expect(out[1]).toBe(76);
    expect(out[2]).toBe(0);
  });

  it('灰度像素在调整后仍保持灰度（±1 舍入）', () => {
    const src = solid(3, 3, 128, 128, 128);
    const out = saturationAdjust(src, 1.5);
    for (let i = 0; i < out.length; i += 4) {
      expect(Math.abs(out[i] - 128)).toBeLessThanOrEqual(1);
      expect(Math.abs(out[i + 1] - 128)).toBeLessThanOrEqual(1);
      expect(Math.abs(out[i + 2] - 128)).toBeLessThanOrEqual(1);
    }
  });

  it('alpha 通道原样保留', () => {
    const buf = solid(2, 2, 200, 100, 50);
    buf[3] = 137;
    const out = saturationAdjust(buf, 2);
    expect(out[3]).toBe(137);
  });

  it('可写入调用方提供的 dst 缓冲', () => {
    const src = solid(2, 2, 200, 100, 50);
    const dst = new Uint8ClampedArray(2 * 2 * 4);
    const out = saturationAdjust(src, 1.2, dst);
    expect(out).toBe(dst);
    expect(dst[0]).toBe(215);
  });
});
