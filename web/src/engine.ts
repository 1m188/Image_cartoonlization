/**
 * 主线程编排器：把一次卡通化任务拆成水平条带，分发给 worker 池并行执行
 * 双边滤波（耗时大头），最后任一 worker 做描边收尾。
 *
 * - 条带读取范围含 halo（radius × iterations），带内结果与整图滤波逐像素一致
 * - 参数在任务开始时快照，导出/预览结果不会中途混入新滑条值
 * - 回退链：worker 池 ≥2 → 单 worker → 主线程 cartoonize（构造失败或池崩溃时）
 * - 新任务到来时 cancel 旧任务：终止全部 worker 立即释放 CPU，
 *   池在下次任务时重建（jobId 校验兜底旧消息串扰）
 */
import { cartoonize, STAGE_WEIGHTS, type CartoonParams } from './pipeline/pipeline';
import { planStrips, type StripRange } from './tiling';
import type { MainToWorkerMessage, WorkerToMainMessage } from './types';

export type ProgressCallback = (fraction: number, stage: string) => void;
export type WorkerFactory = () => Worker;

interface JobState {
  jobId: number;
  kind: 'single' | 'parallel' | 'main';
  width: number;
  height: number;
  /** 任务开始时的参数快照（不随 UI 滑条变化） */
  params: CartoonParams;
  /** 原图引用（不复制），供 worker 池崩溃时主线程重试；任务结束即置空 */
  src: Uint8ClampedArray | null;
  retried: boolean;
  onProgress?: ProgressCallback;
  resolve: (data: Uint8ClampedArray<ArrayBuffer>) => void;
  reject: (err: Error) => void;
  // parallel 专用
  strips: StripRange[];
  pendingStrips: number;
  stripRows: number[];
  totalRows: number;
  stripFractions: Map<number, number>;
  assembled: Uint8ClampedArray | null;
  finishSent: boolean;
}

const ROW_BYTES = (width: number) => width * 4;

function defaultWorkerFactory(): Worker {
  return new Worker(new URL('./worker.ts', import.meta.url), { type: 'module' });
}

export class CartoonEngine {
  private workers: Worker[] = [];
  private jobSeq = 0;
  private job: JobState | null = null;
  private poolBroken = false;

  /** createWorker 仅测试注入用；生产走默认的 module worker 构造 */
  constructor(private createWorker?: WorkerFactory) {}

  async process(
    src: Uint8ClampedArray,
    width: number,
    height: number,
    params: CartoonParams,
    onProgress?: ProgressCallback,
  ): Promise<Uint8ClampedArray<ArrayBuffer>> {
    if (this.job) this.cancel();

    return new Promise<Uint8ClampedArray<ArrayBuffer>>((resolve, reject) => {
      const jobId = ++this.jobSeq;
      const state: JobState = {
        jobId,
        kind: 'main',
        width,
        height,
        params: { ...params }, // 参数快照：任务期间滑条变化不影响本任务
        src,
        retried: false,
        onProgress,
        resolve,
        reject,
        strips: [],
        pendingStrips: 0,
        stripRows: [],
        totalRows: 0,
        stripFractions: new Map(),
        assembled: null,
        finishSent: false,
      };
      this.job = state;

      try {
        this.ensurePool();
        if (this.workers.length >= 2) {
          this.runParallel(state, src);
        } else if (this.workers.length === 1) {
          this.runSingle(state, src);
        } else {
          this.runMainThread(state); // 无 worker 可用：主线程兜底
        }
      } catch (err) {
        this.settleError(state, err instanceof Error ? err : new Error(String(err)));
      }
    });
  }

  /** 取消当前任务：终止所有 worker 立即释放 CPU（池会在下次任务时重建） */
  cancel(): void {
    const job = this.job;
    this.job = null;
    this.terminatePool();
    job?.reject(new Error('cancelled'));
  }

  dispose(): void {
    this.cancel();
  }

  // ---------- 内部实现 ----------

  private ensurePool(): void {
    if (this.poolBroken || this.workers.length) return;
    const factory = this.createWorker ?? defaultWorkerFactory;
    const n = Math.min(Math.max(navigator.hardwareConcurrency || 2, 1), 8);
    const created: Worker[] = [];
    try {
      for (let i = 0; i < n; i++) {
        const w = factory();
        w.onmessage = (e: MessageEvent<WorkerToMainMessage>) => this.onWorkerMessage(e.data);
        w.onerror = (e) => {
          e.preventDefault?.();
          this.onPoolCrash();
        };
        created.push(w);
      }
      if (created.length === 0) throw new Error('未能创建任何 worker');
      this.workers = created;
    } catch {
      for (const w of created) {
        try {
          w.terminate();
        } catch {
          /* 忽略 */
        }
      }
      this.workers = [];
      this.poolBroken = true;
    }
  }

  /** worker 实例崩溃（构造成功但模块加载失败等）：销毁整个池，当前任务主线程重试一次 */
  private onPoolCrash(): void {
    this.terminatePool();
    this.poolBroken = true;
    const state = this.job;
    if (!state || state.kind === 'main') return;
    if (!state.retried) {
      state.retried = true;
      this.runMainThread(state);
    } else {
      this.settleError(state, new Error('worker 池不可用且主线程重试仍失败'));
    }
  }

  private terminatePool(): void {
    for (const w of this.workers) {
      try {
        w.terminate();
      } catch {
        /* 忽略 */
      }
    }
    this.workers = [];
  }

  private postTo(index: number, message: MainToWorkerMessage, transfer: Transferable[]): void {
    const w = this.workers[index];
    if (!w) throw new Error(`worker #${index} 不存在`);
    w.postMessage(message, transfer);
  }

  /** 主线程兜底：直接跑 cartoonize（阻塞主线程，仅在 worker 不可用时触发） */
  private runMainThread(state: JobState): void {
    state.kind = 'main';
    const src = state.src;
    if (!src) {
      this.settleError(state, new Error('无法回退到主线程：原始数据已被释放'));
      return;
    }
    state.onProgress?.(0, '主线程处理（worker 不可用）');
    try {
      // cartoonize 内部新建输出缓冲，运行时必然是独立 ArrayBuffer；此处仅类型收窄
      const out = cartoonize(src, state.width, state.height, state.params, (f, s) => {
        state.onProgress?.(f, s);
      }) as Uint8ClampedArray<ArrayBuffer>;
      state.onProgress?.(1, '完成');
      this.job = null;
      state.src = null;
      state.resolve(out);
    } catch (err) {
      this.settleError(state, err instanceof Error ? err : new Error(String(err)));
    }
  }

  /** 单 worker 完整管线（池大小为 1 时） */
  private runSingle(state: JobState, src: Uint8ClampedArray): void {
    state.kind = 'single';
    const copy = new Uint8ClampedArray(src); // 不分离调用方的缓冲
    const buf = copy.buffer as ArrayBuffer;
    this.postTo(
      0,
      {
        type: 'full',
        jobId: state.jobId,
        width: state.width,
        height: state.height,
        params: state.params,
        buffer: buf,
      },
      [buf],
    );
  }

  /** 并行条带 */
  private runParallel(state: JobState, src: Uint8ClampedArray): void {
    state.kind = 'parallel';
    const iterations = Math.max(1, Math.round(state.params.iterations));
    const halo = Math.max(1, Math.ceil(state.params.radius)) * iterations;

    state.strips = planStrips(state.height, this.workers.length, halo);
    state.pendingStrips = state.strips.length;
    state.stripRows = state.strips.map((s) => s.ry1 - s.ry0);
    state.totalRows = state.stripRows.reduce((a, b) => a + b, 0);
    state.assembled = new Uint8ClampedArray(state.width * state.height * 4);
    state.onProgress?.(STAGE_WEIGHTS.saturation, '双边滤波');

    const rowBytes = ROW_BYTES(state.width);
    state.strips.forEach((s, i) => {
      const start = s.ry0 * rowBytes;
      const len = (s.ry1 - s.ry0) * rowBytes;
      // 从子数组构造 = 复制出独立缓冲，可安全 transfer
      const strip = new Uint8ClampedArray(src.subarray(start, start + len));
      const buf = strip.buffer as ArrayBuffer;
      this.postTo(
        i,
        {
          type: 'strip',
          jobId: state.jobId,
          stripIndex: i,
          width: state.width,
          height: s.ry1 - s.ry0,
          params: state.params,
          buffer: buf,
        },
        [buf],
      );
    });
  }

  private onWorkerMessage(msg: WorkerToMainMessage): void {
    const state = this.job;
    if (!state || msg.jobId !== state.jobId) return; // 已取消的旧任务
    if (state.kind === 'main') return; // 主线程回退中，不再收 worker 消息

    if (msg.type === 'error') {
      this.settleError(state, new Error(msg.message));
      return;
    }

    if (state.kind === 'single') {
      if (msg.type === 'progress') {
        state.onProgress?.(msg.fraction, msg.stage);
      } else if (msg.type === 'done') {
        state.onProgress?.(1, '完成');
        this.job = null;
        state.src = null;
        state.resolve(new Uint8ClampedArray(msg.buffer));
      }
      return;
    }

    // parallel
    if (msg.type === 'stripProgress') {
      state.stripFractions.set(msg.stripIndex, Math.min(1, Math.max(0, msg.fraction)));
      this.reportParallelProgress(state);
      return;
    }

    if (msg.type === 'done' && !state.finishSent) {
      if (msg.stripIndex === undefined || !state.strips[msg.stripIndex]) {
        this.settleError(state, new Error(`条带结果无效（stripIndex=${msg.stripIndex}）`));
        return;
      }
      try {
        this.absorbStrip(state, msg.stripIndex, new Uint8ClampedArray(msg.buffer));
      } catch (err) {
        this.settleError(state, err instanceof Error ? err : new Error(String(err)));
        return;
      }
      state.pendingStrips--;
      if (state.pendingStrips === 0) this.sendFinish(state);
      return;
    }

    if (msg.type === 'done' && state.finishSent) {
      state.onProgress?.(1, '完成');
      this.job = null;
      state.src = null;
      state.resolve(new Uint8ClampedArray(msg.buffer));
    }
  }

  /** 把完成的条带拷入整图缓冲（只取归属行） */
  private absorbStrip(state: JobState, stripIndex: number, strip: Uint8ClampedArray): void {
    const s = state.strips[stripIndex];
    if (!s || !state.assembled) {
      throw new Error(`条带 ${stripIndex} 无法拼装`);
    }
    const rowBytes = ROW_BYTES(state.width);
    const outRows = s.y1 - s.y0;
    const srcOffset = (s.y0 - s.ry0) * rowBytes;
    state.assembled.set(
      strip.subarray(srcOffset, srcOffset + outRows * rowBytes),
      s.y0 * rowBytes,
    );
  }

  private sendFinish(state: JobState): void {
    state.finishSent = true;
    state.onProgress?.(STAGE_WEIGHTS.saturation + STAGE_WEIGHTS.bilateral, '边缘检测');
    const assembled = state.assembled!;
    const buf = assembled.buffer as ArrayBuffer;
    this.postTo(
      0,
      {
        type: 'finish',
        jobId: state.jobId,
        width: state.width,
        height: state.height,
        edgeThresh: state.params.edgeThresh,
        buffer: buf,
      },
      [buf],
    );
  }

  private reportParallelProgress(state: JobState): void {
    let done = 0;
    for (let i = 0; i < state.strips.length; i++) {
      const f = state.stripFractions.get(i) ?? 0;
      done += state.stripRows[i] * f;
    }
    const agg = state.totalRows ? done / state.totalRows : 0;
    const fraction = STAGE_WEIGHTS.saturation + STAGE_WEIGHTS.bilateral * Math.min(1, agg);
    state.onProgress?.(fraction, '双边滤波');
  }

  /** 出错即终止整个池（其余条带不再白算），池在下次任务时重建 */
  private settleError(state: JobState, err: Error): void {
    if (this.job === state) this.job = null;
    state.src = null;
    this.terminatePool();
    state.reject(err);
  }
}
