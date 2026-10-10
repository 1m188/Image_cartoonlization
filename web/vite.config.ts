/// <reference types="vitest/config" />
import { defineConfig } from 'vite';

export default defineConfig({
  // 相对路径，构建产物可放在任意子路径 / 静态托管上直接跑
  base: './',
  build: {
    target: 'es2022',
  },
  worker: {
    format: 'es',
  },
  test: {
    environment: 'node',
    include: ['tests/**/*.test.ts'],
  },
});
