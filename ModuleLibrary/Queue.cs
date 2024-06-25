using System;
using System.Collections;
using System.Collections.Generic;

namespace ModuleLibrary;

public class Queue<T> : IEnumerable<T>
{
    Node<T> _head;
    Node<T> _tail;
    int _count;
    public int Count => _count;
    public bool IsEmpty => _count == 0;
    public bool IsFilled => !IsEmpty;

    public void Enqueue(T data)
    {
        Node<T> node = new(data);
        Node<T> tempNode = _tail;
        _tail = node;
        if (_count == 0)
            _head = _tail;
        else
            tempNode.Next = _tail;
        _count++;
    }

    public void Enqueue(IEnumerable<T> values)
    {
        var curdata = values.GetEnumerator();
        while (curdata.MoveNext())
        {
            Enqueue(curdata.Current);
        }
    }

    public T Dequeue()
    {
        if (this.IsEmpty)
            throw new InvalidOperationException();
        T output = _head.Data;
        _head = _head.Next;
        _count--;
        return output;
    }

    public T First
    {
        get
        {
            if (this.IsEmpty)
                throw new InvalidOperationException();
            return _head.Data;
        }
    }

    public T Last
    {
        get
        {
            if (this.IsEmpty)
                throw new InvalidOperationException();
            return _tail.Data;
        }
    }

    public void Clear()
    {
        _head = null;
        _tail = null;
        _count = 0;
    }

    public bool Contains(T data)
    {
        Node<T> current = _head;
        while (current != null)
        {
            if (current.Data.Equals(data))
                return true;
            current = current.Next;
        }
        return false;
    }
    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)this).GetEnumerator();
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        Node<T> current = _head;
        while (current != null)
        {
            yield return current.Data;
            current = current.Next;
        }
    }
}
