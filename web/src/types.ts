import type { CartoonParams } from './pipeline/pipeline';

/** 主线程 → worker */
export type MainToWorkerMessage =
  | {
      /** 完整管线（单 worker 模式 / 并行不可用时的回退） */
      type: 'full';
      jobId: number;
      width: number;
      height: number;
      params: CartoonParams;
      /** RGBA 像素，所有权转移给 worker */
      buffer: ArrayBuffer;
    }
  | {
      /** 水平条带：饱和度 + 双边滤波 × iterations（不含描边） */
      type: 'strip';
      jobId: number;
      stripIndex: number;
      width: number;
      height: number;
      params: CartoonParams;
      /** 条带 RGBA 像素（含 halo 行），所有权转移给 worker */
      buffer: ArrayBuffer;
    }
  | {
      /** 收尾：灰度 → Sobel 边缘 → 掩膜置黑 */
      type: 'finish';
      jobId: number;
      width: number;
      height: number;
      edgeThresh: number;
      /** 整图滤波结果 RGBA，所有权转移给 worker */
      buffer: ArrayBuffer;
    };

/** worker → 主线程 */
export type WorkerToMainMessage =
  | { type: 'progress'; jobId: number; fraction: number; stage: string }
  | { type: 'stripProgress'; jobId: number; stripIndex: number; fraction: number }
  | { type: 'done'; jobId: number; stripIndex?: number; buffer: ArrayBuffer }
  | { type: 'error'; jobId: number; message: string };
