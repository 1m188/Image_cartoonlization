package cartoon

import (
	"fmt"
	"math"
	"time"
)

// Params 定义卡通化处理的所有可调参数。
type Params struct {
	EdgeThresh float64 // 边缘检测阈值（默认 0.02，范围 0.0~1.0，越小边缘越多）
	SatScalar  float64 // 饱和度增益（默认 2.0，1.0=不变，>1 增强，<1 减弱）
	Radius     int     // 双边滤波窗口半径（默认 10，窗口大小 = 2*Radius+1）
	SigmaD     float64 // 空间域标准差 σ_d（默认 3.0，越大空间越模糊）
	SigmaR     float64 // 颜色域标准差 σ_r（默认 0.1，越大颜色越模糊）
	LoopNum    int     // 双边滤波迭代次数（默认 1，越大越模糊）
	Workers    int     // 并行 worker 数量（0 表示使用 CPU 核数）
}

// DefaultParams 返回一组默认参数，与 MATLAB 原版一致。
func DefaultParams() Params {
	return Params{
		EdgeThresh: 0.02,
		SatScalar:  2.0,
		Radius:     10,
		SigmaD:     3.0,
		SigmaR:     0.1,
		LoopNum:    1,
		Workers:    0,
	}
}

// StepResult 记录一个处理步骤的结果。
type StepResult struct {
	Name     string
	Duration time.Duration
	Err      error
	Detail   string
}

// Cartoonize 对输入图片执行完整的卡通化流水线。
//
// 流程：
//  1. 饱和度调整
//  2. 双边滤波（至少 1 次，loop_num 控制次数）
//  3. 第一次双边滤波后进行边缘检测
//  4. 边缘叠加为黑色描边
//
// 返回每一步的处理结果（仅在 verbose 模式被调用方使用）。
func Cartoonize(img [][][]float64, p Params) ([][][]float64, []StepResult, error) {
	var steps []StepResult

	if len(img) == 0 || len(img[0]) == 0 {
		return nil, steps, fmt.Errorf("输入图片为空")
	}

	start := time.Now()

	// ── 步骤 1：饱和度调整 ─────────────────────────────────
	t0 := time.Now()
	img = AdjustSaturation(img, p.SatScalar)
	steps = append(steps, StepResult{
		Name:     "饱和度调整",
		Duration: time.Since(t0),
		Detail:   fmt.Sprintf("s=%.2f", p.SatScalar),
	})

	// ── 步骤 2：第一次双边滤波 ─────────────────────────────
	t1 := time.Now()
	img = BilateralFilter(img, p.Radius, p.SigmaD, p.SigmaR, p.Workers)
	steps = append(steps, StepResult{
		Name:     "双边滤波 #1",
		Duration: time.Since(t1),
		Detail: fmt.Sprintf("σ_d=%.2f σ_r=%.2f radius=%d workers=%d",
			p.SigmaD, p.SigmaR, p.Radius, p.Workers),
	})

	// ── 步骤 3：边缘检测 ───────────────────────────────────
	t2 := time.Now()
	gray := RGBToGray(img)
	edgeMask := DetectEdges(gray, p.EdgeThresh)

	edgeCount := 0
	for i := range edgeMask {
		for j := range edgeMask[i] {
			if edgeMask[i][j] > 0.5 {
				edgeCount++
			}
		}
	}
	totalPixels := len(edgeMask) * len(edgeMask[0])
	edgePct := float64(edgeCount) / float64(max(totalPixels, 1)) * 100.0

	steps = append(steps, StepResult{
		Name:     "边缘检测",
		Duration: time.Since(t2),
		Detail:   fmt.Sprintf("sobel threshold=%.4f edges=%.1f%%", p.EdgeThresh, edgePct),
	})

	// ── 步骤 4：额外双边滤波迭代 ──────────────────────────
	for i := 2; i <= p.LoopNum; i++ {
		t3 := time.Now()
		img = BilateralFilter(img, p.Radius, p.SigmaD, p.SigmaR, p.Workers)
		steps = append(steps, StepResult{
			Name:     fmt.Sprintf("双边滤波 #%d", i),
			Duration: time.Since(t3),
			Detail: fmt.Sprintf("σ_d=%.2f σ_r=%.2f radius=%d workers=%d",
				p.SigmaD, p.SigmaR, p.Radius, p.Workers),
		})
	}

	// ── 步骤 5：边缘叠加 ───────────────────────────────────
	t4 := time.Now()
	img = OverlayEdges(img, edgeMask)
	steps = append(steps, StepResult{
		Name:     "边缘叠加",
		Duration: time.Since(t4),
		Detail:   fmt.Sprintf("%d 边缘像素已叠加", edgeCount),
	})

	// ── 步骤 6：最终钳制 ───────────────────────────────────
	for i := range img {
		for j := range img[i] {
			for c := 0; c < 3; c++ {
				img[i][j][c] = math.Max(0, math.Min(1, img[i][j][c]))
			}
		}
	}

	steps = append(steps, StepResult{
		Name:     "总处理",
		Duration: time.Since(start),
		Detail:   "",
	})

	return img, steps, nil
}
