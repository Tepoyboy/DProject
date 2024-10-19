using UnityEngine;
using System.Linq; 
using System.Collections.Generic;

public class InputBuffer<T> {
    public struct BufferedInput {
        public T Input;
        public float TimeStamp;
    }
    private Queue<BufferedInput> bufferQueue = new Queue<BufferedInput>();
    public IReadOnlyCollection<BufferedInput> BufferQueue => bufferQueue;
    public float bufferTime;

    public InputBuffer(float bufferTime) {
        this.bufferTime = bufferTime;
    }

    public T ReturnLastInput() {
        if(BufferQueue.Count > 0){
            return bufferQueue.Last().Input;
        }

        return default;
    }

    public T ReturnFirstInput(){
        if(BufferQueue.Count > 0){
            return bufferQueue.Peek().Input;
        }

        return default;
    }

    public void AddInput(T input) {
        bufferQueue.Enqueue(new BufferedInput {
            Input = input,
            TimeStamp = Time.time
        });
    }

    /* public bool IsRunning() {
        float currentTime = Time.time;
        foreach (var bufferedInput in bufferQueue) {
            if (currentTime - bufferedInput.TimeStamp <= bufferTime)
            {
                return true;
            }
        }
        return false;
    } */

    public void InputCleaner() {
        if(BufferQueue.Count <= 0) return;
        float currentTime = Time.time;
        // Debug.Log("INPUT CLEANER : bufferQueue.Count = " + bufferQueue.Count);

        while (bufferQueue.Count > 0) {
            BufferedInput bufferedInput = bufferQueue.Peek();
            float duration = currentTime - bufferedInput.TimeStamp;
            if (duration > bufferTime) {
                bufferQueue.Dequeue();
                // Debug.Log($"{bufferedInput.Input} is cleaning {duration} seconds");
            }
            else {
                break;
            }
        }
    }

    /* public bool ConsumeInput(out T input) {
        float currentTime = Time.time;
        if (bufferQueue.Count > 0)
        {
            BufferedInput bufferedInput = bufferQueue.Peek();
            if (currentTime - bufferedInput.TimeStamp <= bufferTime)
            {
                input = bufferedInput.Input;
                bufferQueue.Dequeue();
                return true;
            }
        }
        input = default;
        return false;
    } */

    public void Clear() {
        bufferQueue.Clear();
    }
}
