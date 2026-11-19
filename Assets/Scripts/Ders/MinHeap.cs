using System.Collections.Generic;

/// <summary>Hafta 4 – A* ve Dijkstra'nın açık listesi için ikili min-yığın (öncelik kuyruğu).</summary>
public class MinHeap<T>
{
    readonly List<(T item, float key, int order)> data = new List<(T, float, int)>();
    int counter;   // eşitlikte önce giren önce çıksın

    public int Count => data.Count;

    public void Push(T item, float key)
    {
        data.Add((item, key, counter++));
        int i = data.Count - 1;
        while (i > 0)
        {
            int p = (i - 1) / 2;
            if (!Less(i, p)) break;
            (data[i], data[p]) = (data[p], data[i]);
            i = p;
        }
    }

    public T Pop()
    {
        T top = data[0].item;
        int last = data.Count - 1;
        data[0] = data[last];
        data.RemoveAt(last);
        int i = 0;
        while (true)
        {
            int l = 2 * i + 1, r = l + 1, m = i;
            if (l < data.Count && Less(l, m)) m = l;
            if (r < data.Count && Less(r, m)) m = r;
            if (m == i) break;
            (data[i], data[m]) = (data[m], data[i]);
            i = m;
        }
        return top;
    }

    bool Less(int a, int b) =>
        data[a].key < data[b].key || (data[a].key == data[b].key && data[a].order < data[b].order);
}
