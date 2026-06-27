package cartoon

import "math"

// DetectEdges 使用 Sobel 算子检测灰度图中的边缘。
//
// 算法流程：
//  1. 3×3 Sobel 水平/垂直卷积（边界像素零填充）
//  2. 梯度幅值 G = sqrt(Gx² + Gy²)
//  3. G > threshold → 标记为边缘
//
// 返回值为 [][]float64，其中边缘像素为 1.0，非边缘像素为 0.0。
func DetectEdges(gray [][]float64, threshold float64) [][]float64 {
	height := len(gray)
	if height < 3 {
		// 图片太小，返回全零掩码
		out := make([][]float64, height)
		for i := range out {
			out[i] = make([]float64, len(gray[i]))
		}
		return out
	}
	width := len(gray[0])
	if width < 3 {
		out := make([][]float64, height)
		for i := range out {
			out[i] = make([]float64, width)
		}
		return out
	}

	// Sobel 核
	sobelX := [3][3]float64{
		{-1, 0, 1},
		{-2, 0, 2},
		{-1, 0, 1},
	}
	sobelY := [3][3]float64{
		{1, 2, 1},
		{0, 0, 0},
		{-1, -2, -1},
	}

	edge := make([][]float64, height)
	for i := 0; i < height; i++ {
		edge[i] = make([]float64, width)
	}

	// 计算梯度幅值（跳过边界一行/一列以避免越界，边界默认为非边缘）
	for i := 1; i < height-1; i++ {
		for j := 1; j < width-1; j++ {
			var gx, gy float64
			for di := -1; di <= 1; di++ {
				for dj := -1; dj <= 1; dj++ {
					val := gray[i+di][j+dj]
					gx += val * sobelX[di+1][dj+1]
					gy += val * sobelY[di+1][dj+1]
				}
			}
			g := math.Sqrt(gx*gx + gy*gy)
			if g > threshold {
				edge[i][j] = 1.0
			}
		}
	}

	return edge
}

// OverlayEdges 将边缘掩码叠加到模糊后的图片上。
//
// 边缘像素（mask=1）变为黑色（RGB=0），非边缘像素保持不变。
// 与 MATLAB 中 img_blur - img_blur .* edge_mask 完全一致。
func OverlayEdges(blurred [][][]float64, edgeMask [][]float64) [][][]float64 {
	height := len(blurred)
	width := len(blurred[0])
	out := make([][][]float64, height)

	for i := 0; i < height; i++ {
		out[i] = make([][]float64, width)
		for j := 0; j < width; j++ {
			out[i][j] = make([]float64, 3)
			if edgeMask[i][j] > 0.5 {
				// 边缘像素 → 黑色
				out[i][j][0] = 0
				out[i][j][1] = 0
				out[i][j][2] = 0
			} else {
				out[i][j][0] = clamp01(blurred[i][j][0])
				out[i][j][1] = clamp01(blurred[i][j][1])
				out[i][j][2] = clamp01(blurred[i][j][2])
			}
		}
	}
	return out
}
