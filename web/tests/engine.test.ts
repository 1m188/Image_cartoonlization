import { describe, expect, it, vi } from 'vitest';
import { CartoonEngine } from '../src/engine';
import { processFinish, processStrip } from '../src/jobs';
import { DEFAULT_PARAMS, cartoonize, type CartoonParams } from '../src/pipeline/pipeline';

/**
 * Fake worker：在测试进程内模拟 worker.ts 的消息处理（共用 jobs.ts 内核）。
 * - ok：正常异步应答
 * - crash：第一次 postMessage 后触发 onerror（模拟模块加载失败）
 * - error：条带处理时回 error 消息
 * - stall：永不应答（测取消）
 */
class FakeWorker {
  onmessage: ((e: { data: unknown }) => void) | null = null;
  onerror: ((e: { preventDefault(): void }) => void) | null = null;
  terminated = false;

  constructor(private mode: 'ok' | 'crash' | 'error' | 'stall' = 'ok') {}

  postMessage(msg: Record<string, unknown>): void {
    if (this.terminated) return;
    if (this.mode === 'stall') return;
    if (this.mode === 'crash') {
      setTimeout(() => this.onerror?.({ preventDefault() {} }), 0);
      return;
    }
    setTimeout(() => this.handle(msg), 0);
  }

  private handle(msg: Record<string, unknown>): void {
    if (this.terminated) return;
    const jobId = msg.jobId as number;
    try {
      if (msg.type === 'full') {
        // 单 worker 模式（池大小 1 时 engine 走此路径）
        const src = new Uint8ClampedArray(msg.buffer as ArrayBuffer);
        const out = cartoonize(
          src,
          msg.width as number,
          msg.height as number,
          msg.params as CartoonParams,
          (fraction, stage) => {
            this.onmessage?.({ data: { type: 'progress', jobId, fraction, stage } });
          },
        );
        this.onmessage?.({ data: { type: 'done', jobId, buffer: out.buffer } });
        return;
      }
      if (msg.type === 'strip') {
        if (this.mode === 'error') {
          this.onmessage?.({ data: { type: 'error', jobId, message: '注入的条带错误' } });
          return;
        }
        const src = new Uint8ClampedArray(msg.buffer as ArrayBuffer);
        const out = processStrip(
          src,
          msg.width as number,
          msg.height as number,
          msg.params as CartoonParams,
          (fraction) => {
            this.onmessage?.({
              data: {
                type: 'stripProgress',
                jobId,
                stripIndex: msg.stripIndex,
                fraction,
              },
            });
          },
        );
        this.onmessage?.({
          data: { type: 'done', jobId, stripIndex: msg.stripIndex, buffer: out.buffer },
        });
        return;
      }
      if (msg.type === 'finish') {
        const filtered = new Uint8ClampedArray(msg.buffer as ArrayBuffer);
        const out = processFinish(filtered, msg.width as number, msg.height as number, msg.edgeThresh as number);
        this.onmessage?.({ data: { type: 'done', jobId, buffer: out.buffer } });
        return;
      }
    } catch (err) {
      this.onmessage?.({
        data: { type: 'error', jobId, message: err instanceof Error ? err.message : String(err) },
      });
    }
  }

  terminate(): void {
    this.terminated = true;
  }
}

function makeEngine(mode: 'ok' | 'crash' | 'error' | 'stall' = 'ok'): CartoonEngine {
  return new CartoonEngine(() => new FakeWorker(mode) as unknown as Worker);
}

/** 生成确定性的伪随机测试图 */
function noiseImage(w: number, h: number): Uint8ClampedArray {
  const buf = new Uint8ClampedArray(w * h * 4);
  let seed = 42;
  const rand = () => {
    seed = (seed * 1103515245 + 12345) & 0x7fffffff;
    return seed / 0x7fffffff;
  };
  for (let i = 0; i < w * h; i++) {
    buf[i * 4] = Math.round(rand() * 255);
    buf[i * 4 + 1] = Math.round(rand() * 255);
    buf[i * 4 + 2] = Math.round(rand() * 255);
    buf[i * 4 + 3] = 255;
  }
  return buf;
}

const PARAMS: CartoonParams = { ...DEFAULT_PARAMS, radius: 4, sigmaD: 2, sigmaR: 0.1 };

describe('CartoonEngine 并行编排', () => {
  it('并行条带拼装结果与单线程 cartoonize 逐像素一致（iterations=1）', async () => {
    const w = 48;
    const h = 32;
    const src = noiseImage(w, h);
    const engine = makeEngine();
    const out = await engine.process(src, w, h, PARAMS);
    const expected = cartoonize(src, w, h, PARAMS);
    expect(Array.from(out)).toEqual(Array.from(expected));
    engine.dispose();
  });

  it('iterations=2 且 radius>height（单带退化）时仍与单线程一致', async () => {
    const w = 40;
    const h = 8;
    const src = noiseImage(w, h);
    const engine = makeEngine();
    const params = { ...PARAMS, radius: 12, iterations: 2 };
    const out = await engine.process(src, w, h, params);
    const expected = cartoonize(src, w, h, params);
    expect(Array.from(out)).toEqual(Array.from(expected));
    engine.dispose();
  });

  it('参数在任务开始时快照：调用方随后改参数不影响结果', async () => {
    const w = 32;
    const h = 32;
    const src = noiseImage(w, h);
    const engine = makeEngine();
    const params = { ...PARAMS };
    const pending = engine.process(src, w, h, params);
    params.edgeThresh = 0.5; // 任务进行中拖动滑条
    params.saturation = 3;
    const out = await pending;
    const expected = cartoonize(src, w, h, { ...PARAMS });
    expect(Array.from(out)).toEqual(Array.from(expected));
    engine.dispose();
  });

  it('上报进度并到达 (1, 完成)', async () => {
    const w = 32;
    const h = 32;
    const src = noiseImage(w, h);
    const engine = makeEngine();
    const onProgress = vi.fn();
    await engine.process(src, w, h, PARAMS, onProgress);
    expect(onProgress).toHaveBeenCalled();
    const last = onProgress.mock.calls.at(-1)!;
    expect(last[0]).toBe(1);
    expect(last[1]).toBe('完成');
    engine.dispose();
  });

  it('cancel() 使进行中的任务以 cancelled 拒绝', async () => {
    const w = 32;
    const h = 32;
    const src = noiseImage(w, h);
    const engine = makeEngine('stall');
    const pending = engine.process(src, w, h, PARAMS);
    const expectation = expect(pending).rejects.toThrow('cancelled');
    engine.cancel();
    await expectation;
  });

  it('worker 报错时任务以该错误拒绝', async () => {
    const w = 32;
    const h = 32;
    const src = noiseImage(w, h);
    const engine = makeEngine('error');
    await expect(engine.process(src, w, h, PARAMS)).rejects.toThrow('注入的条带错误');
    engine.dispose();
  });

  it('worker 池崩溃时自动回退主线程并得到正确结果', async () => {
    const w = 48;
    const h = 32;
    const src = noiseImage(w, h);
    const engine = makeEngine('crash');
    const out = await engine.process(src, w, h, PARAMS);
    const expected = cartoonize(src, w, h, PARAMS);
    expect(Array.from(out)).toEqual(Array.from(expected));
    engine.dispose();
  });
});
