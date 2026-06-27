package cartoon

import (
	"math"
	"sync"
)

// bilatColor 对 CIELab 彩色图执行双边滤波（行级并行）。
//
// 与 MATLAB bfltColor.m 一致：sigma_r 在 CIELab 空间会乘以 100，
// 使默认 σ_r=0.1 对应有效 σ_r=10（与 L 通道 0~100 范围匹配）。
func bilatColor(img [][][]float64, w int, sigmaD, sigmaR float64, workers int) [][][]float64 {
	height := len(img)
	width := len(img[0])
	kernelSize := 2*w + 1

	// 预计算空间域高斯核
	domainKernel := make([][]float64, kernelSize)
	for dx := -w; dx <= w; dx++ {
		domainKernel[dx+w] = make([]float64, kernelSize)
		for dy := -w; dy <= w; dy++ {
			domainKernel[dx+w][dy+w] = math.Exp(-float64(dx*dx+dy*dy) / (2 * sigmaD * sigmaD))
		}
	}

	// 与 MATLAB 一致：在 CIELab 空间放大 σ_r
	sigmaR = 100 * sigmaR

	out := make([][][]float64, height)
	for i := range out {
		out[i] = make([][]float64, width)
		for j := range out[i] {
			out[i][j] = make([]float64, 3)
		}
	}

	rowsPerWorker := (height + workers - 1) / workers
	var wg sync.WaitGroup

	for wk := 0; wk < workers; wk++ {
		startRow := wk * rowsPerWorker
		endRow := min(startRow+rowsPerWorker, height)
		if startRow >= height {
			break
		}

		wg.Add(1)
		go func(start, end int) {
			defer wg.Done()
			for i := start; i < end; i++ {
				for j := 0; j < width; j++ {
					iMin := max(i-w, 0)
					iMax := min(i+w, height-1)
					jMin := max(j-w, 0)
					jMax := min(j+w, width-1)

					curL := img[i][j][0]
					curA := img[i][j][1]
					curB := img[i][j][2]

					var sumL, sumA, sumB, sumW float64

					for pi := iMin; pi <= iMax; pi++ {
						for pj := jMin; pj <= jMax; pj++ {
							dL := img[pi][pj][0] - curL
							dA := img[pi][pj][1] - curA
							dB := img[pi][pj][2] - curB
							rangeDiff := dL*dL + dA*dA + dB*dB
							rangeW := math.Exp(-rangeDiff / (2 * sigmaR * sigmaR))

							domainW := domainKernel[pi-i+w][pj-j+w]
							weight := rangeW * domainW

							sumL += weight * img[pi][pj][0]
							sumA += weight * img[pi][pj][1]
							sumB += weight * img[pi][pj][2]
							sumW += weight
						}
					}

					out[i][j][0] = sumL / sumW
					out[i][j][1] = sumA / sumW
					out[i][j][2] = sumB / sumW
				}
			}
		}(startRow, endRow)
	}

	wg.Wait()
	return out
}
