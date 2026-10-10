import { describe, expect, it, vi } from 'vitest';
import { bilateralFilterRGB } from '../src/pipeline/bilateral';

function horizontalTwoTone(w: number, h: number, left: number, right: number): Uint8ClampedArray {
  const buf = new Uint8ClampedArray(w * h * 4);
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const v = x < w / 2 ? left : right;
      const i = (y * w + x) * 4;
      buf[i] = v;
      buf[i + 1] = v;
      buf[i + 2] = v;
      buf[i + 3] = 255;
    }
  }
  return buf;
}

describe('bilateralFilterRGB（对应 bfltColor.m，Lab 空间）', () => {
  it('常量灰度图滤波后不变（权重归一化，±1 舍入）', () => {
    const w = 16;
    const h = 16;
    const src = horizontalTwoTone(w, h, 128, 128);
    const out = bilateralFilterRGB(src, w, h, { radius: 5, sigmaD: 2, sigmaR: 0.1 });
    for (let i = 0; i < out.length; i += 4) {
      expect(Math.abs(out[i] - 128)).toBeLessThanOrEqual(1);
      expect(Math.abs(out[i + 1] - 128)).toBeLessThanOrEqual(1);
      expect(Math.abs(out[i + 2] - 128)).toBeLessThanOrEqual(1);
    }
  });

  it('σr 小（默认量级）时强边缘被保留：远离边界处两侧各自保持原值', () => {
    const w = 32;
    const h = 8;
    const src = horizontalTwoTone(w, h, 30, 220);
    const out = bilateralFilterRGB(src, w, h, { radius: 5, sigmaD: 2, sigmaR: 0.1 });
    // 左侧深处（x=3）与右侧深处（x=28）
    const li = (4 * w + 3) * 4;
    const ri = (4 * w + 28) * 4;
    expect(out[li]).toBeLessThanOrEqual(35);
    expect(out[ri]).toBeGreaterThanOrEqual(215);
  });

  it('σr 极大时退化为近似高斯平滑：边缘附近向另一侧融合', () => {
    const w = 32;
    const h = 1;
    const src = horizontalTwoTone(w, h, 30, 220);
    const out = bilateralFilterRGB(src, w, h, { radius: 4, sigmaD: 2, sigmaR: 2 });
    // x=13 的窗口 [9..17] 跨越 x=16 的边界
    const li = 13 * 4;
    expect(out[li]).toBeGreaterThan(40);
    expect(out[li]).toBeLessThan(210);
  });

  it('σr 小时同样的位置几乎不融合（对比用例）', () => {
    const w = 32;
    const h = 1;
    const src = horizontalTwoTone(w, h, 30, 220);
    const out = bilateralFilterRGB(src, w, h, { radius: 4, sigmaD: 2, sigmaR: 0.1 });
    expect(out[13 * 4]).toBeLessThanOrEqual(35);
  });

  it('输出尺寸与输入一致，alpha 全 255', () => {
    const w = 8;
    const h = 8;
    const src = horizontalTwoTone(w, h, 10, 200);
    const out = bilateralFilterRGB(src, w, h, { radius: 3, sigmaD: 1.5, sigmaR: 0.1 });
    expect(out.length).toBe(src.length);
    for (let i = 3; i < out.length; i += 4) expect(out[i]).toBe(255);
  });

  it('窗口大于图像时（边界大量钳制）常量图输出不变', () => {
    const w = 3;
    const h = 3;
    const src = horizontalTwoTone(w, h, 90, 90);
    const out = bilateralFilterRGB(src, w, h, { radius: 5, sigmaD: 3, sigmaR: 0.1 });
    for (let i = 0; i < out.length; i += 4) {
      expect(Math.abs(out[i] - 90)).toBeLessThanOrEqual(1);
      expect(Math.abs(out[i + 1] - 90)).toBeLessThanOrEqual(1);
      expect(Math.abs(out[i + 2] - 90)).toBeLessThanOrEqual(1);
    }
  });

  it('通过 onProgress 上报 (0,1] 的进度', () => {
    const w = 16;
    const h = 16;
    const src = horizontalTwoTone(w, h, 30, 220);
    const onProgress = vi.fn();
    bilateralFilterRGB(src, w, h, { radius: 3, sigmaD: 1.5, sigmaR: 0.1 }, onProgress);
    expect(onProgress).toHaveBeenCalled();
    const calls = onProgress.mock.calls.map((c) => c[0] as number);
    expect(Math.max(...calls)).toBeCloseTo(1, 5);
    for (const f of calls) {
      expect(f).toBeGreaterThan(0);
      expect(f).toBeLessThanOrEqual(1);
    }
  });
});
