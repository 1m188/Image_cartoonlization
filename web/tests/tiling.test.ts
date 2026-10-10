import { describe, expect, it } from 'vitest';
import { planStrips } from '../src/tiling';

describe('planStrips（并行滤波的行分带规划）', () => {
  it('所有带的归属行无缝覆盖 [0, height)', () => {
    const strips = planStrips(1000, 8, 10);
    expect(strips[0].y0).toBe(0);
    for (let i = 1; i < strips.length; i++) {
      expect(strips[i].y0).toBe(strips[i - 1].y1);
    }
    expect(strips[strips.length - 1].y1).toBe(1000);
  });

  it('读取范围 = 归属行 ± halo，且钳制在图像内', () => {
    const strips = planStrips(1000, 8, 10);
    for (const s of strips) {
      expect(s.ry0).toBe(Math.max(0, s.y0 - 10));
      expect(s.ry1).toBe(Math.min(1000, s.y1 + 10));
      expect(s.ry0).toBeGreaterThanOrEqual(0);
      expect(s.ry1).toBeLessThanOrEqual(1000);
    }
    // 首尾带边界钳制
    expect(strips[0].ry0).toBe(0);
    expect(strips[strips.length - 1].ry1).toBe(1000);
  });

  it('带数不超过行数（每带至少 1 行）', () => {
    const strips = planStrips(3, 8, 1); // halo=1 不触发单带退化（3 ≥ 2×1）
    expect(strips.length).toBe(3);
    for (const s of strips) expect(s.y1 - s.y0).toBeGreaterThanOrEqual(1);
  });

  it('行数小于 2×halo 时退化为单带（保证 halo 语义成立）', () => {
    const strips = planStrips(15, 8, 10);
    expect(strips.length).toBe(1);
    expect(strips[0]).toEqual({ y0: 0, y1: 15, ry0: 0, ry1: 15 });
  });

  it('带数同时受 height/halo 约束，避免极窄带的冗余 halo 复制', () => {
    // 91 行、halo 45：若允许 8 带 → 每带 11 行却要读 91 行（~9× 冗余）
    const strips = planStrips(91, 8, 45);
    expect(strips.length).toBe(2);
    expect(strips[0].y0).toBe(0);
    expect(strips[1].y1).toBe(91);
  });

  it('height=0 返回空', () => {
    expect(planStrips(0, 4, 10)).toEqual([]);
  });

  it('count=1 时单带覆盖全图', () => {
    expect(planStrips(100, 1, 5)).toEqual([
      { y0: 0, y1: 100, ry0: 0, ry1: 100 },
    ]);
  });
});
