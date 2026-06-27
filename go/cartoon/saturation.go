package cartoon

// AdjustSaturation 调整图片饱和度。
//
// 使用线性内插/外推公式：
//
//	result = (1 - scalar) × gray + scalar × color
//
// 当 scalar = 1.0 时，输出与输入一致（不变）；
// 当 scalar > 1.0 时，颜色被外推远离灰度，饱和度增强；
// 当 scalar < 1.0 时，颜色向灰度收拢，饱和度降低。
//
// img 为 [height][width][3]float64，像素值范围 [0.0, 1.0]。
// 返回值同样为 [0.0, 1.0] 范围。
func AdjustSaturation(img [][][]float64, scalar float64) [][][]float64 {
	height := len(img)
	width := len(img[0])
	out := make([][][]float64, height)

	for i := 0; i < height; i++ {
		out[i] = make([][]float64, width)
		for j := 0; j < width; j++ {
			// 计算灰度值（与 MATLAB rgb2gray 一致）
			gray := 0.299*img[i][j][0] + 0.587*img[i][j][1] + 0.114*img[i][j][2]

			out[i][j] = []float64{
				clamp01((1-scalar)*gray + scalar*img[i][j][0]),
				clamp01((1-scalar)*gray + scalar*img[i][j][1]),
				clamp01((1-scalar)*gray + scalar*img[i][j][2]),
			}
		}
	}
	return out
}
