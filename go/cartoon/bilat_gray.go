package cartoon

import (
	"math"
	"sync"
)

// bilatGray 对单通道灰度图执行双边滤波（行级并行）。
func bilatGray(img [][]float64, w int, sigmaD, sigmaR float64, workers int) [][]float64 {
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

	out := make([][]float64, height)
	for i := range out {
		out[i] = make([]float64, width)
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

					curVal := img[i][j]
					var sumVal, sumW float64

					for pi := iMin; pi <= iMax; pi++ {
						for pj := jMin; pj <= jMax; pj++ {
							diff := img[pi][pj] - curVal
							rangeW := math.Exp(-(diff * diff) / (2 * sigmaR * sigmaR))
							domainW := domainKernel[pi-i+w][pj-j+w]
							weight := rangeW * domainW

							sumVal += weight * img[pi][pj]
							sumW += weight
						}
					}
					out[i][j] = sumVal / sumW
				}
			}
		}(startRow, endRow)
	}

	wg.Wait()
	return out
}
