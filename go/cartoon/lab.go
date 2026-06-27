package cartoon

import "math"

// D65 参考白点的 CIE XYZ 三刺激值。
const (
	xn = 0.95047
	yn = 1.00000
	zn = 1.08883
)

// delta 为 f(t) 分段函数的边界阈值：(6/29)^3。
const delta = 6.0 / 29.0
const deltaCubed = delta * delta * delta // ≈ 0.008856

// fInv 为反向 f(t) 的边界阈值：(6/29)^2。
const deltaInv = delta * delta // ≈ 0.0428

// f 函数：将线性分量映射为 CIELab 使用的非线性分量。
func f(t float64) float64 {
	if t > deltaCubed {
		return math.Cbrt(t)
	}
	// t/(3*(6/29)^2) + 4/29 = t/(3*delta^2) + 4/29
	return t/(3.0*deltaInv) + 4.0/29.0
}

// fInverse 为 f 的逆函数。
func fInverse(t float64) float64 {
	if t > delta {
		return t * t * t
	}
	return 3.0 * deltaInv * (t - 4.0/29.0)
}

// sRGBToLinear 将 sRGB 分量转为线性分量（去 gamma）。
func sRGBToLinear(c float64) float64 {
	if c <= 0.04045 {
		return c / 12.92
	}
	return math.Pow((c+0.055)/1.055, 2.4)
}

// linearToSRGB 将线性分量转为 sRGB 分量（加 gamma）。
func linearToSRGB(c float64) float64 {
	if c <= 0.0031308 {
		return 12.92 * c
	}
	return 1.055*math.Pow(c, 1.0/2.4) - 0.055
}

// RGBToLab 将 RGB 色彩空间图片（[0,1]）转换为 CIELab 色彩空间。
// 输出图片仍为 [height][width][3]float64，三个通道分别为 L, a, b。
func RGBToLab(img [][][]float64) [][][]float64 {
	height := len(img)
	width := len(img[0])
	out := make([][][]float64, height)

	for i := 0; i < height; i++ {
		out[i] = make([][]float64, width)
		for j := 0; j < width; j++ {
			// sRGB → 线性 RGB
			rLin := sRGBToLinear(img[i][j][0])
			gLin := sRGBToLinear(img[i][j][1])
			bLin := sRGBToLinear(img[i][j][2])

			// 线性 RGB → XYZ
			x := 0.4124564*rLin + 0.3575761*gLin + 0.1804375*bLin
			y := 0.2126729*rLin + 0.7151522*gLin + 0.0721750*bLin
			z := 0.0193339*rLin + 0.1191920*gLin + 0.9503041*bLin

			// XYZ → CIELab
			l := 116.0*f(y/yn) - 16.0
			a := 500.0 * (f(x/xn) - f(y/yn))
			b := 200.0 * (f(y/yn) - f(z/zn))

			out[i][j] = []float64{l, a, b}
		}
	}
	return out
}

// LabToRGB 将 CIELab 色彩空间图片转换回 RGB 色彩空间（[0,1]）。
// 输入图片为 [height][width][3]float64，三个通道分别为 L, a, b。
func LabToRGB(img [][][]float64) [][][]float64 {
	height := len(img)
	width := len(img[0])
	out := make([][][]float64, height)

	for i := 0; i < height; i++ {
		out[i] = make([][]float64, width)
		for j := 0; j < width; j++ {
			l := img[i][j][0]
			a := img[i][j][1]
			b := img[i][j][2]

			// CIELab → XYZ
			fy := (l + 16.0) / 116.0
			fx := fy + a/500.0
			fz := fy - b/200.0

			xx := xn * fInverse(fx)
			yy := yn * fInverse(fy)
			zz := zn * fInverse(fz)

			// XYZ → 线性 RGB
			rLin := 3.2404542*xx - 1.5371385*yy - 0.4985314*zz
			gLin := -0.9692660*xx + 1.8760108*yy + 0.0415560*zz
			bLin := 0.0556434*xx - 0.2040259*yy + 1.0572252*zz

			// 钳制到有效范围再转 sRGB
			rLin = clamp01(rLin)
			gLin = clamp01(gLin)
			bLin = clamp01(bLin)

			out[i][j] = []float64{
				linearToSRGB(rLin),
				linearToSRGB(gLin),
				linearToSRGB(bLin),
			}
		}
	}
	return out
}

// clamp01 将数值钳制到 [0.0, 1.0] 范围。
func clamp01(v float64) float64 {
	if v < 0 {
		return 0
	}
	if v > 1 {
		return 1
	}
	return v
}

// RGBToGray 将 RGB 图片转为灰度图 [][]float64。
// 使用 ITU-R BT.601 亮度系数。
func RGBToGray(img [][][]float64) [][]float64 {
	height := len(img)
	width := len(img[0])
	gray := make([][]float64, height)
	for i := 0; i < height; i++ {
		gray[i] = make([]float64, width)
		for j := 0; j < width; j++ {
			gray[i][j] = 0.299*img[i][j][0] + 0.587*img[i][j][1] + 0.114*img[i][j][2]
		}
	}
	return gray
}
