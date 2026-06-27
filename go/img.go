package main

import (
	"fmt"
	"image"
	"image/jpeg"
	"image/png"
	"os"
	"path/filepath"
	"strings"
)

// LoadImage 从指定路径加载图片，返回统一内部格式 [height][width][3]float64，像素值范围 [0.0, 1.0]。
// 支持的输入格式：JPEG（.jpg / .jpeg）、PNG（.png）。
func LoadImage(path string) ([][][]float64, error) {
	f, err := os.Open(path)
	if err != nil {
		return nil, fmt.Errorf("无法打开文件 %q: %w", path, err)
	}
	defer f.Close()

	img, format, err := image.Decode(f)
	if err != nil {
		return nil, fmt.Errorf("无法解码图片 %q: %w", path, err)
	}

	if format != "jpeg" && format != "png" {
		return nil, fmt.Errorf("不支持的图片格式 %q，仅支持 JPEG 和 PNG", format)
	}

	bounds := img.Bounds()
	height := bounds.Dy()
	width := bounds.Dx()
	if height == 0 || width == 0 {
		return nil, fmt.Errorf("图片尺寸异常：width=%d, height=%d", width, height)
	}

	data := make([][][]float64, height)
	for y := 0; y < height; y++ {
		data[y] = make([][]float64, width)
		for x := 0; x < width; x++ {
			r, g, b, _ := img.At(x+bounds.Min.X, y+bounds.Min.Y).RGBA()
			data[y][x] = []float64{
				float64(r) / 65535.0,
				float64(g) / 65535.0,
				float64(b) / 65535.0,
			}
		}
	}

	return data, nil
}

// SaveImage 将内部格式 [height][width][3]float64 保存到指定路径。
// 输出格式由文件后缀决定：.jpg / .jpeg → JPEG（质量 95），.png → PNG。
func SaveImage(path string, data [][][]float64) error {
	ext := strings.ToLower(filepath.Ext(path))
	var format string
	switch ext {
	case ".jpg", ".jpeg":
		format = "jpeg"
	case ".png":
		format = "png"
	default:
		return fmt.Errorf("不支持输出后缀 %q，仅支持 .jpg / .jpeg / .png", ext)
	}

	height := len(data)
	if height == 0 {
		return fmt.Errorf("输出数据为空")
	}
	width := len(data[0])

	// 转换为 image.NRGBA
	img := image.NewNRGBA(image.Rect(0, 0, width, height))
	for y := 0; y < height; y++ {
		for x := 0; x < width; x++ {
			r := clampUint8(data[y][x][0] * 255.0)
			g := clampUint8(data[y][x][1] * 255.0)
			b := clampUint8(data[y][x][2] * 255.0)
			idx := y*img.Stride + x*4
			img.Pix[idx+0] = r
			img.Pix[idx+1] = g
			img.Pix[idx+2] = b
			img.Pix[idx+3] = 255
		}
	}

	f, err := os.Create(path)
	if err != nil {
		return fmt.Errorf("无法创建输出文件 %q: %w", path, err)
	}
	defer f.Close()

	switch format {
	case "jpeg":
		err = jpeg.Encode(f, img, &jpeg.Options{Quality: 95})
	case "png":
		err = png.Encode(f, img)
	}

	if err != nil {
		return fmt.Errorf("编码输出图片失败: %w", err)
	}

	return nil
}

// clampUint8 将 float64 钳制到 [0, 255] 并转为 uint8。
func clampUint8(v float64) uint8 {
	v = v + 0.5 // 四舍五入
	if v < 0 {
		return 0
	}
	if v > 255 {
		return 255
	}
	return uint8(v)
}
