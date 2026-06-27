package main

import (
	"flag"
	"fmt"
	"os"
	"path/filepath"
	"strings"
	"time"

	"image_cartoonlization/cartoon"
)

// 版本号。
const version = "1.0.0"

func main() {
	os.Exit(run())
}

func run() int {
	// ── 命令行参数定义 ──────────────────────────────────────
	var (
		inPath      = flag.String("i", "", "输入图片路径（必填，支持 JPEG / PNG）")
		outPath     = flag.String("o", "cartoon.png", "输出图片路径（默认 cartoon.png，支持 JPEG / PNG）")
		edgeThresh  = flag.Float64("edge-thresh", 0.02, "边缘检测阈值（0.0~1.0，越小边缘越多）")
		satScalar   = flag.Float64("sat", 2.0, "饱和度增益（1.0=不变，>1 增强，<1 减弱）")
		radius      = flag.Int("radius", 10, "双边滤波窗口半径（1~50，窗口大小=2*radius+1）")
		sigmaD      = flag.Float64("sigma-d", 3.0, "空间域标准差 σ_d（越大越模糊）")
		sigmaR      = flag.Float64("sigma-r", 0.1, "颜色域标准差 σ_r（越大越模糊）")
		loopNum     = flag.Int("loop", 1, "双边滤波迭代次数（1~10）")
		workersFlag = flag.Int("workers", 0, "并行 worker 数量（0=CPU 核数）")
		verbose     = flag.Bool("v", false, "详细输出模式，打印每一步的处理信息")
		showHelp    = flag.Bool("help", false, "显示完整帮助信息")
		showHelpH   = flag.Bool("h", false, "显示完整帮助信息")
	)

	flag.Parse()

	// ── 帮助信息 ───────────────────────────────────────────
	if *showHelp || *showHelpH {
		printHelp()
		return 0
	}

	// ── 参数校验 ───────────────────────────────────────────
	if *inPath == "" {
		fmt.Fprintln(os.Stderr, "错误：缺少输入图片路径，使用 -i 指定")
		fmt.Fprintln(os.Stderr, "使用 -h 查看完整帮助信息")
		return 1
	}

	if *edgeThresh < 0 || *edgeThresh > 1 {
		fmt.Fprintln(os.Stderr, "错误：--edge-thresh 必须在 0.0 ~ 1.0 之间")
		return 1
	}
	if *satScalar < 0 {
		fmt.Fprintln(os.Stderr, "错误：--sat 必须 >= 0")
		return 1
	}
	if *radius < 1 || *radius > 50 {
		fmt.Fprintln(os.Stderr, "错误：--radius 必须在 1 ~ 50 之间")
		return 1
	}
	if *sigmaD <= 0 {
		fmt.Fprintln(os.Stderr, "错误：--sigma-d 必须 > 0")
		return 1
	}
	if *sigmaR <= 0 {
		fmt.Fprintln(os.Stderr, "错误：--sigma-r 必须 > 0")
		return 1
	}
	if *loopNum < 1 || *loopNum > 10 {
		fmt.Fprintln(os.Stderr, "错误：--loop 必须在 1 ~ 10 之间")
		return 1
	}
	if *workersFlag < 0 {
		fmt.Fprintln(os.Stderr, "错误：--workers 必须 >= 0")
		return 1
	}

	// ── 输出文件后缀校验 ──────────────────────────────────
	ext := strings.ToLower(filepath.Ext(*outPath))
	if ext != ".jpg" && ext != ".jpeg" && ext != ".png" {
		fmt.Fprintf(os.Stderr, "错误：输出路径 %q 后缀不支持，仅支持 .jpg / .jpeg / .png\n", ext)
		return 1
	}

	// ── 加载输入图片 ───────────────────────────────────────
	loadStart := time.Now()
	img, err := LoadImage(*inPath)
	if err != nil {
		fmt.Fprintf(os.Stderr, "[加载] 失败: %v\n", err)
		return 1
	}
	loadDur := time.Since(loadStart)

	if *verbose {
		fmt.Printf("[加载] 成功 | %s | %d×%d | %v\n",
			filepath.Base(*inPath), len(img[0]), len(img), roundDuration(loadDur))
	}

	// ── 执行卡通化处理 ─────────────────────────────────────
	params := cartoon.Params{
		EdgeThresh: *edgeThresh,
		SatScalar:  *satScalar,
		Radius:     *radius,
		SigmaD:     *sigmaD,
		SigmaR:     *sigmaR,
		LoopNum:    *loopNum,
		Workers:    *workersFlag,
	}

	img, steps, err := cartoon.Cartoonize(img, params)
	if err != nil {
		fmt.Fprintf(os.Stderr, "[错误] 处理失败: %v\n", err)
		return 1
	}

	// ── Verbose 输出 ───────────────────────────────────────
	if *verbose {
		for _, s := range steps {
			if s.Name == "总处理" {
				continue
			}
			status := "成功"
			if s.Err != nil {
				status = fmt.Sprintf("失败: %v", s.Err)
			}
			line := fmt.Sprintf("[%s] %s | %v", s.Name, status, roundDuration(s.Duration))
			if s.Detail != "" {
				line += " | " + s.Detail
			}
			fmt.Println(line)
		}
	}

	// ── 保存输出图片 ───────────────────────────────────────
	saveStart := time.Now()
	if err := SaveImage(*outPath, img); err != nil {
		fmt.Fprintf(os.Stderr, "[输出] 失败: %v\n", err)
		return 1
	}
	saveDur := time.Since(saveStart)

	if *verbose {
		fmt.Printf("[输出] 成功 | %s | %v\n", filepath.Base(*outPath), roundDuration(saveDur))
	}

	// 查找并输出总耗时
	for _, s := range steps {
		if s.Name == "总处理" {
			fmt.Printf("[完成] 总耗时 %v\n", roundDuration(s.Duration))
			break
		}
	}

	return 0
}

// roundDuration 将耗时四舍五入到毫秒级别显示。
func roundDuration(d time.Duration) time.Duration {
	return d.Round(time.Millisecond)
}

// printHelp 输出完整的中文帮助信息。
func printHelp() {
	fmt.Print(`image_cartoonlization ` + version + ` — 照片卡通化命令行工具

将照片转换为卡通风格。通过饱和度增强、双边滤波保边平滑、边缘检测描边
等技术，模拟手绘卡通效果。

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

用法:
  image_cartoonlization -i <输入图片> [-o <输出图片>] [选项]

必选参数:
  -i <路径>            输入图片路径（支持 JPEG / PNG 格式）

可选参数:
  -o <路径>            输出图片路径
                       默认: cartoon.png
                       后缀 .jpg / .jpeg → JPEG 编码（质量 95）
                       后缀 .png         → PNG 编码（无损）

  --edge-thresh <值>   边缘检测阈值
                       默认: 0.02
                       范围: 0.0 ~ 1.0
                       越小 → 检测到的边缘越多，描边越密
                       越大 → 只保留强边缘，描边越稀疏

  --sat <值>           饱和度增益
                       默认: 2.0
                       1.0 = 不改变饱和度
                       >1 = 增强饱和度（推荐 1.5 ~ 3.0）
                       <1 = 降低饱和度

  --radius <值>        双边滤波窗口半径
                       默认: 10
                       范围: 1 ~ 50
                       窗口实际大小为 (2 × radius + 1) 像素
                       越大 → 平滑范围越大，计算越慢

  --sigma-d <值>       空间域标准差
                       默认: 3.0
                       控制像素空间距离对滤波的影响
                       越大 → 远处像素也参与平均，越模糊

  --sigma-r <值>       颜色域标准差
                       默认: 0.1
                       控制像素颜色差异对滤波的影响
                       越大 → 不同颜色的像素也参与平均，越模糊
                       越小 → 边缘保持越好

  --loop <值>          双边滤波迭代次数
                       默认: 1
                       范围: 1 ~ 10
                       每次迭代都会进一步平滑图像
                       越大 → 越模糊，计算时间成倍增加

  --workers <值>       并行 worker 数量
                       默认: 0（自动使用 CPU 核数）
                       设为 1 禁用并行处理
                       设为 N 使用 N 个并行 worker

  -v                   详细输出模式
                       打印每一步的处理耗时和参数信息

  -h, --help           显示本帮助信息

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

使用示例:

  # 基本用法：将 photo.jpg 转换为 cartoon.png（默认参数）
  image_cartoonlization -i photo.jpg -o cartoon.png

  # 增强边缘效果：降低阈值，增加描边密度
  image_cartoonlization -i photo.jpg -o cartoon.jpg --edge-thresh 0.005

  # 更模糊的卡通效果：增大窗口和迭代次数
  image_cartoonlization -i photo.jpg -o cartoon.png --radius 15 --loop 3

  # 鲜艳色彩 + 弱描边
  image_cartoonlization -i photo.jpg -o cartoon.png --sat 3 --edge-thresh 0.05

  # 使用单线程 + 详细输出
  image_cartoonlization -i photo.jpg -o cartoon.png --workers 1 -v

  # 输出为 JPEG 格式
  image_cartoonlization -i photo.png -o cartoon.jpg

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

支持的图片格式:
  输入: JPEG (.jpg / .jpeg)、PNG (.png)
  输出: JPEG (.jpg / .jpeg)、PNG (.png)

处理流程:
  加载 → 饱和度调整 → 双边滤波（保边平滑）→ Sobel 边缘检测
       → 边缘叠加（黑色描边）→ 保存输出

技术说明:
  本项目从 MATLAB 原版（Image_cartoonlization）移植到 Go。
  双边滤波在 CIELab 色彩空间中执行，以更好地匹配人眼感知。
  边缘检测使用 Sobel 算子进行 3×3 卷积，取梯度幅值后二值化。
`)
}
