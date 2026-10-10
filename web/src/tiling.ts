/**
 * 并行滤波的行分带规划（纯函数，可单测）。
 *
 * 双边滤波的窗口半径为 w（+ 多轮迭代时为 w×iterations），只要每带的
 * 读取范围包含归属行 ± halo，带内滤波结果与整图滤波逐像素一致
 * （边界复制语义在带内与全图相同）。
 */
export interface StripRange {
  /** 本带负责输出的行范围 [y0, y1) */
  y0: number;
  y1: number;
  /** 需要从原图读取的行范围 [ry0, ry1)，已含 halo 并钳制到图像内 */
  ry0: number;
  ry1: number;
}

/**
 * 把 height 行分成至多 count 个带。
 * - 行数不足以摊薄 halo 开销（height < 2×halo）时退化为单带；
 * - 带数同时受 height/halo 约束（每带归属行 ≥ halo），
 *   避免「带略大于 2×halo 时切出大量极窄带」的冗余计算。
 */
export function planStrips(height: number, count: number, halo: number): StripRange[] {
  if (height <= 0) return [];
  const effectiveCount =
    height < 2 * halo
      ? 1
      : Math.min(count, Math.max(1, Math.floor(height / halo)));

  const strips: StripRange[] = [];
  const rowsPerStrip = Math.floor(height / effectiveCount);
  let y0 = 0;
  for (let i = 0; i < effectiveCount; i++) {
    // 最后一带吸收余数
    const y1 = i === effectiveCount - 1 ? height : y0 + rowsPerStrip;
    strips.push({
      y0,
      y1,
      ry0: Math.max(0, y0 - halo),
      ry1: Math.min(height, y1 + halo),
    });
    y0 = y1;
  }
  return strips;
}
