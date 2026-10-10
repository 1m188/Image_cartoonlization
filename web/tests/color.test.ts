import { describe, expect, it } from 'vitest';
import { GRAY_WEIGHTS, labToRgb, rgb2gray, rgb2gray01, rgbToLab } from '../src/pipeline/color';

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

describe('rgb2gray（Rec.601 权重，与 MATLAB rgb2gray 一致）', () => {
  it('按 0.2989/0.587/0.114 加权', () => {
    const src = solid(1, 1, 200, 100, 50);
    const gray = rgb2gray(src);
    const expected = 0.2989 * 200 + 0.587 * 100 + 0.114 * 50; // 124.18
    expect(gray[0]).toBeCloseTo(expected, 2);
  });

  it('rgb2gray01 输出归一到 [0,1]（权重和 0.9999，与 MATLAB 一致，白像素≈0.99990）', () => {
    const src = solid(1, 1, 255, 255, 255);
    expect(rgb2gray01(src)[0]).toBeCloseTo(1, 3);
    const black = solid(1, 1, 0, 0, 0);
    expect(rgb2gray01(black)[0]).toBeCloseTo(0, 5);
  });

  it('导出的权重常量与 MATLAB 一致', () => {
    expect([...GRAY_WEIGHTS]).toEqual([0.2989, 0.587, 0.114]);
  });
});

describe('rgbToLab（D65 白点，对应 makecform(\'srgb2lab\')）', () => {
  it('白色 → L=100, a=0, b=0', () => {
    const lab = rgbToLab(solid(1, 1, 255, 255, 255), 1);
    expect(lab[0]).toBeCloseTo(100, 3);
    expect(lab[1]).toBeCloseTo(0, 3);
    expect(lab[2]).toBeCloseTo(0, 3);
  });

  it('黑色 → L=0, a=0, b=0', () => {
    const lab = rgbToLab(solid(1, 1, 0, 0, 0), 1);
    expect(lab[0]).toBeCloseTo(0, 3);
    expect(lab[1]).toBeCloseTo(0, 3);
    expect(lab[2]).toBeCloseTo(0, 3);
  });

  it('纯红 → 标准 Lab 值 (53.24, 80.09, 67.20)', () => {
    const lab = rgbToLab(solid(1, 1, 255, 0, 0), 1);
    expect(lab[0]).toBeCloseTo(53.24, 1);
    expect(lab[1]).toBeCloseTo(80.09, 1);
    expect(lab[2]).toBeCloseTo(67.2, 1);
  });

  it('输出长度 = 像素数 × 3，按像素连续排列', () => {
    const lab = rgbToLab(solid(3, 2, 10, 20, 30), 6);
    expect(lab.length).toBe(18);
  });
});

describe('labToRgb / rgbToLab 往返', () => {
  it('常见颜色往返误差 ≤ 2/255', () => {
    const colors: Array<[number, number, number]> = [
      [255, 255, 255],
      [0, 0, 0],
      [255, 0, 0],
      [0, 255, 0],
      [0, 0, 255],
      [200, 100, 50],
      [128, 128, 128],
      [17, 203, 88],
    ];
    const src = new Uint8ClampedArray(colors.length * 4);
    colors.forEach(([r, g, b], i) => {
      src[i * 4] = r;
      src[i * 4 + 1] = g;
      src[i * 4 + 2] = b;
      src[i * 4 + 3] = 255;
    });
    const lab = rgbToLab(src, colors.length);
    const back = labToRgb(lab, colors.length);
    for (let i = 0; i < colors.length; i++) {
      expect(Math.abs(back[i * 4] - colors[i][0])).toBeLessThanOrEqual(2);
      expect(Math.abs(back[i * 4 + 1] - colors[i][1])).toBeLessThanOrEqual(2);
      expect(Math.abs(back[i * 4 + 2] - colors[i][2])).toBeLessThanOrEqual(2);
      expect(back[i * 4 + 3]).toBe(255);
    }
  });
});
