/**
 * 边缘检测 —— 对应 MATLAB edge(img_gray, 'sobel', thresh)
 *
 * 阈值语义与 MATLAB 一致：thresh < 1 时为**相对阈值**，
 * 即阈值为 thresh × 全图最大梯度幅值（edge.m 的 "% Normalize the threshold" 行为）。
 * 低对比照片不会因固定归一化系数而漏检边缘。
 *
 * 视觉近似（按需求「效果接近即可」）：
 * - 非极大值抑制用 22.5° 四扇区（MATLAB 内部为 6:1 硬扇区），边缘位置基本一致
 * - NMS 后保留局部极大 → 1px 细边缘
 */
function at(gray: Float32Array, width: number, height: number, x: number, y: number): number {
  const cx = x < 0 ? 0 : x >= width ? width - 1 : x;
  const cy = y < 0 ? 0 : y >= height ? height - 1 : y;
  return gray[cy * width + cx];
}

export function edgeMask(
  gray: Float32Array,
  width: number,
  height: number,
  thresh: number,
  dst?: Uint8Array,
): Uint8Array {
  const mask = dst ?? new Uint8Array(width * height);
  mask.fill(0);

  // 第一遍：梯度幅值 + 方向量化（0°/45°/90°/135°），并记录全图最大幅值
  const mag = new Float32Array(width * height);
  const dir = new Uint8Array(width * height);
  let maxMag = 0;
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const tl = at(gray, width, height, x - 1, y - 1);
      const tc = at(gray, width, height, x, y - 1);
      const tr = at(gray, width, height, x + 1, y - 1);
      const ml = at(gray, width, height, x - 1, y);
      const mr = at(gray, width, height, x + 1, y);
      const bl = at(gray, width, height, x - 1, y + 1);
      const bc = at(gray, width, height, x, y + 1);
      const br = at(gray, width, height, x + 1, y + 1);

      // Sobel gx（右为正）与 gy（下为正）
      const gx = -tl + tr - 2 * ml + 2 * mr - bl + br;
      const gy = -tl - 2 * tc - tr + bl + 2 * bc + br;

      const p = y * width + x;
      const m = Math.sqrt(gx * gx + gy * gy);
      mag[p] = m;
      if (m > maxMag) maxMag = m;

      // 梯度方向量化到 4 个扇区（0°=水平梯度→垂直边）
      const ang = (Math.atan2(gy, gx) * 180) / Math.PI;
      const a = ang < 0 ? ang + 180 : ang; // [0,180)
      dir[p] = a < 22.5 ? 0 : a < 67.5 ? 1 : a < 112.5 ? 2 : a < 157.5 ? 3 : 0;
    }
  }

  // 相对阈值（MATLAB edge(): thresh × 全图最大梯度；maxMag=0 的平坦图无边缘）
  const cutoff = thresh * maxMag;

  // 第二遍：非极大值抑制 + 阈值
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const p = y * width + x;
      const m = mag[p];
      if (m <= cutoff) continue;

      // 沿梯度方向的两个相邻像素
      let n1: number;
      let n2: number;
      switch (dir[p]) {
        case 0: // 梯度水平 → 比较左右
          n1 = at2(mag, width, height, x - 1, y);
          n2 = at2(mag, width, height, x + 1, y);
          break;
        case 1: // 45°
          n1 = at2(mag, width, height, x + 1, y - 1);
          n2 = at2(mag, width, height, x - 1, y + 1);
          break;
        case 2: // 梯度垂直 → 比较上下
          n1 = at2(mag, width, height, x, y - 1);
          n2 = at2(mag, width, height, x, y + 1);
          break;
        default: // 135°
          n1 = at2(mag, width, height, x - 1, y - 1);
          n2 = at2(mag, width, height, x + 1, y + 1);
          break;
      }
      if (m >= n1 && m >= n2) mask[p] = 1;
    }
  }

  return mask;
}

function at2(mag: Float32Array, width: number, height: number, x: number, y: number): number {
  const cx = x < 0 ? 0 : x >= width ? width - 1 : x;
  const cy = y < 0 ? 0 : y >= height ? height - 1 : y;
  return mag[cy * width + cx];
}
