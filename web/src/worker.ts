/**
 * 计算 worker：三种消息——完整管线 / 水平条带（饱和度+双边滤波）/ 描边收尾。
 * 由 engine.ts（主线程）编排：并行时按条带分发给多个本 worker 实例。
 * 计算内核在 jobs.ts（与编排层测试共用）。
 */
import { cartoonize } from './pipeline/pipeline';
import { processFinish, processStrip } from './jobs';
import type { MainToWorkerMessage, WorkerToMainMessage } from './types';

const ctx = self as unknown as {
  postMessage(message: WorkerToMainMessage, transfer?: Transferable[]): void;
  onmessage: ((e: MessageEvent<MainToWorkerMessage>) => void) | null;
};

function post(message: WorkerToMainMessage, transfer?: Transferable[]): void {
  ctx.postMessage(message, transfer);
}

ctx.onmessage = (e: MessageEvent<MainToWorkerMessage>) => {
  const msg = e.data;
  try {
    if (msg.type === 'full') {
      const src = new Uint8ClampedArray(msg.buffer);
      const out = cartoonize(src, msg.width, msg.height, msg.params, (fraction, stage) => {
        post({ type: 'progress', jobId: msg.jobId, fraction, stage });
      });
      post({ type: 'done', jobId: msg.jobId, buffer: out.buffer as ArrayBuffer }, [
        out.buffer,
      ]);
      return;
    }

    if (msg.type === 'strip') {
      const src = new Uint8ClampedArray(msg.buffer);
      const out = processStrip(src, msg.width, msg.height, msg.params, (fraction) => {
        post({
          type: 'stripProgress',
          jobId: msg.jobId,
          stripIndex: msg.stripIndex,
          fraction,
        });
      });
      post(
        {
          type: 'done',
          jobId: msg.jobId,
          stripIndex: msg.stripIndex,
          buffer: out.buffer as ArrayBuffer,
        },
        [out.buffer],
      );
      return;
    }

    if (msg.type === 'finish') {
      const filtered = new Uint8ClampedArray(msg.buffer);
      const out = processFinish(filtered, msg.width, msg.height, msg.edgeThresh);
      post({ type: 'done', jobId: msg.jobId, buffer: out.buffer as ArrayBuffer }, [
        out.buffer,
      ]);
      return;
    }
  } catch (err) {
    post({
      type: 'error',
      jobId: (msg as { jobId?: number }).jobId ?? -1,
      message: err instanceof Error ? err.message : String(err),
    });
  }
};
