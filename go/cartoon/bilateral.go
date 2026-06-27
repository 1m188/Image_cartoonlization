package cartoon

import (
	"runtime"
)

// BilateralFilter 对彩色图片执行双边滤波。
//
// 内部先将 RGB 转换到 CIELab 色彩空间进行滤波，再转换回 RGB。
// 如果输入为单通道灰度图，则直接使用灰度双边滤波。
//
// 参数:
//   - img:   输入图片 [height][width][channels]float64，值域 [0.0, 1.0]
//   - w:     窗口半径（窗口大小为 2w+1）
//   - sigmaD: 空间域标准差 σ_d
//   - sigmaR: 颜色域标准差 σ_r（在 CIELab 空间会被放大 100 倍，与 MATLAB 一致）
//   - workers: 并行 goroutine 数量（0 表示使用 CPU 核数）
func BilateralFilter(img [][][]float64, w int, sigmaD, sigmaR float64, workers int) [][][]float64 {
	if workers <= 0 {
		workers = runtime.NumCPU()
	}

	if len(img) == 0 || len(img[0]) == 0 {
		return img
	}

	channels := len(img[0][0])
	if channels == 1 {
		// 灰度图路径
		gray := make([][]float64, len(img))
		for i := range img {
			gray[i] = make([]float64, len(img[i]))
			for j := range img[i] {
				gray[i][j] = img[i][j][0]
			}
		}
		gray = bilatGray(gray, w, sigmaD, sigmaR, workers)
		out := make([][][]float64, len(img))
		for i := range out {
			out[i] = make([][]float64, len(img[i]))
			for j := range out[i] {
				out[i][j] = []float64{gray[i][j]}
			}
		}
		return out
	}

	// 彩色图路径：RGB → Lab → 双边滤波 → RGB
	lab := RGBToLab(img)
	lab = bilatColor(lab, w, sigmaD, sigmaR, workers)
	return LabToRGB(lab)
}
