//! 图像数据存储模块。
//!
//! `ImageData` 使用行优先、通道交错的扁平 `Vec<f64>` 存储，
//! 比三级指针的 `Vec<Vec<[f64;3]>>` 具有更好的缓存局部性。
//!
//! 索引公式: `(y * width + x) * channels + c`

/// 图像数据容器，像素值域为 [0.0, 1.0]。
#[derive(Clone)]
pub struct ImageData {
    /// 图像宽度（像素）
    pub width: usize,
    /// 图像高度（像素）
    pub height: usize,
    /// 通道数（1=灰度, 3=RGB/Lab）
    pub channels: usize,
    /// 扁平存储的行优先像素数据
    pub data: Vec<f64>,
}

impl ImageData {
    /// 创建指定尺寸的零填充图像。
    pub fn new(width: usize, height: usize, channels: usize) -> Self {
        let size = width * height * channels;
        ImageData {
            width,
            height,
            channels,
            data: vec![0.0; size],
        }
    }

    /// 计算 (y, x, c) 像素在扁平数组中的索引。
    /// 行优先，通道交错排列（RGBRGB...）。
    pub fn index(&self, y: usize, x: usize, c: usize) -> usize {
        debug_assert!(
            y < self.height && x < self.width && c < self.channels,
            "index out of bounds: ({},{},{}) for image {}x{}x{}",
            y,
            x,
            c,
            self.width,
            self.height,
            self.channels
        );
        (y * self.width + x) * self.channels + c
    }

    /// 获取指定像素指定通道的值。
    /// 越界时 `debug_assert` 会在 debug 模式下 panic。
    pub fn get(&self, y: usize, x: usize, c: usize) -> f64 {
        self.data[self.index(y, x, c)]
    }

    /// 设置指定像素指定通道的值。
    pub fn set(&mut self, y: usize, x: usize, c: usize, value: f64) {
        let idx = self.index(y, x, c);
        self.data[idx] = value;
    }

    /// 返回 (y, x) 像素第一个通道在底层数组中的偏移量。
    /// 可用于批量读写同一像素的三个通道，避免重复计算索引。
    pub fn pixel_offset(&self, y: usize, x: usize) -> usize {
        debug_assert!(
            y < self.height && x < self.width,
            "pixel_offset out of bounds: ({},{}) for image {}x{}",
            y,
            x,
            self.width,
            self.height
        );
        (y * self.width + x) * self.channels
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_new_creates_zero_filled() {
        let img = ImageData::new(2, 3, 3);
        assert_eq!(img.width, 2);
        assert_eq!(img.height, 3);
        assert_eq!(img.channels, 3);
        assert_eq!(img.data.len(), 2 * 3 * 3);
        for &v in &img.data {
            assert_eq!(v, 0.0);
        }
    }

    #[test]
    fn test_index_calculation() {
        let img = ImageData::new(4, 3, 3);
        assert_eq!(img.index(0, 0, 0), 0);
        assert_eq!(img.index(0, 0, 1), 1);
        assert_eq!(img.index(0, 0, 2), 2);
        assert_eq!(img.index(0, 1, 0), 3);
        assert_eq!(img.index(1, 0, 0), 12);
        assert_eq!(img.index(2, 3, 2), 2 * 4 * 3 + 3 * 3 + 2);
    }

    #[test]
    fn test_get_set_roundtrip() {
        let mut img = ImageData::new(3, 2, 3);
        img.set(0, 1, 2, 0.75);
        assert_eq!(img.get(0, 1, 2), 0.75);
        img.set(1, 2, 0, 0.5);
        assert_eq!(img.get(1, 2, 0), 0.5);
    }

    #[test]
    fn test_pixel_offset() {
        let img = ImageData::new(4, 3, 3);
        assert_eq!(img.pixel_offset(0, 0), 0);
        assert_eq!(img.pixel_offset(0, 1), 3);
        assert_eq!(img.pixel_offset(1, 0), 12);
        assert_eq!(img.pixel_offset(2, 3), 2 * 4 * 3 + 3 * 3);
    }

    #[test]
    #[should_panic(expected = "index out of bounds")]
    fn test_get_out_of_bounds_panics() {
        let img = ImageData::new(2, 2, 3);
        img.get(2, 0, 0);
    }
}
