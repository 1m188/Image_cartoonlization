import { describe, expect, it } from 'vitest';
import { edgeMask } from '../src/pipeline/edges';

function grayImage(w: number, h: number, fill: (x: number, y: number) => number): Float32Array {
  const buf = new Float32Array(w * h);
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) buf[y * w + x] = fill(x, y);
  }
  return buf;
}

describe('edgeMask（对应 edge(img_gray, \'sobel\', thresh)）', () => {
  it('平坦图像没有任何边缘', () => {
    const gray = grayImage(16, 16, () => 0.5);
    const mask = edgeMask(gray, 16, 16, 0.02);
    for (let i = 0; i < mask.length; i++) expect(mask[i]).toBe(0);
  });

  it('竖直阶跃边缘：NMS 后每行只保留边缘附近 1~2 像素', () => {
    const w = 32;
    const h = 16;
    const gray = grayImage(w, h, (x) => (x < 16 ? 0.1 : 0.9));
    const mask = edgeMask(gray, w, h, 0.1);
    for (let y = 1; y < h - 1; y++) {
      const hitColumns: number[] = [];
      for (let x = 0; x < w; x++) if (mask[y * w + x] === 1) hitColumns.push(x);
      expect(hitColumns.length).toBeGreaterThanOrEqual(1);
      expect(hitColumns.length).toBeLessThanOrEqual(2);
      for (const x of hitColumns) {
        expect(x).toBeGreaterThanOrEqual(14);
        expect(x).toBeLessThanOrEqual(17);
      }
    }
  });

  it('相对阈值语义：弱边（相对强度 12.5%）在阈值 0.3 下被抑制，强边保留', () => {
    const w = 32;
    const h = 16;
    // 上半：强阶跃 0.1→0.9（定义全图最大梯度）；下半：弱阶跃 0.45→0.55
    const gray = grayImage(w, h, (x, y) => {
      if (y < 8) return x < 16 ? 0.1 : 0.9;
      return x < 16 ? 0.45 : 0.55;
    });
    const mask = edgeMask(gray, w, h, 0.3);
    // 上半强边：每行 1~2 像素
    for (let y = 1; y < 7; y++) {
      const hits: number[] = [];
      for (let x = 0; x < w; x++) if (mask[y * w + x] === 1) hits.push(x);
      expect(hits.length, `strong row ${y}`).toBeGreaterThanOrEqual(1);
      expect(hits.length, `strong row ${y}`).toBeLessThanOrEqual(2);
    }
    // 下半弱边：0.4/3.2 = 12.5% < 30% → 全部抑制
    for (let y = 9; y < 15; y++) {
      for (let x = 0; x < w; x++) expect(mask[y * w + x], `weak row ${y}, x ${x}`).toBe(0);
    }
  });

  it('阈值 0.02（MATLAB 默认量级）能检出阶跃边缘', () => {
    const w = 32;
    const h = 8;
    const gray = grayImage(w, h, (x) => (x < 16 ? 0.1 : 0.9));
    const mask = edgeMask(gray, w, h, 0.02);
    let sum = 0;
    for (let i = 0; i < mask.length; i++) sum += mask[i];
    expect(sum).toBeGreaterThan(0);
  });
});
