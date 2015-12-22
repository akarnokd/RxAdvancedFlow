using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.queues
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct MpscStructLinkedQueue<T>
    {
        long p00, p01, p02, p03, p04, p05, p06, p07;
        long p08, p09, p0A, p0B, p0C, p0D, p0E, p0F;

        internal Node producerNode;

        long p10, p11, p12, p13, p14, p15, p16, p17;
        long p18, p19, p1A, p1B, p1C, p1D, p1E;

        internal Node consumerNode;

        long p20, p21, p22, p23, p24, p25, p26, p27;
        long p28, p29, p2A, p2B, p2C, p2D, p2E;

        internal void Init()
        {
            Node n = new Node();
            consumerNode = n;

            XchgProducerNode(n);
        }

        Node XchgProducerNode(Node n)
        {
            return Interlocked.Exchange(ref producerNode, n);
        }

        internal bool Offer(T value)
        {
            Node newNode = new Node(value);

            return Offer(newNode);
        }

        internal bool Offer(Node node)
        {
            Node prevNode = XchgProducerNode(node);

            prevNode.SoNext(node);

            return true;
        }

        internal bool Poll(out T value)
        {
            Node currConsumerNode = consumerNode;

            Node nextNode = currConsumerNode.LvNext();

            if (nextNode != null)
            {
                value = nextNode.LpValueAndFree();
                consumerNode = nextNode;
                return true;
            }
            else
            if (currConsumerNode != Volatile.Read(ref producerNode))
            {
                while ((nextNode = currConsumerNode.LvNext()) != null) ;

                value = nextNode.LpValueAndFree();
                consumerNode = nextNode;
                return true;
            }
            value = default(T);
            return false;
        }

        internal void DropWeak()
        {
            Node n = consumerNode.LpNext();
            n.Free();
            consumerNode = n;
        }

        internal void Drop()
        {
            Node currConsumerNode = consumerNode;

            Node nextNode = currConsumerNode.LvNext();

            if (nextNode != null)
            {
                nextNode.Free();
                consumerNode = nextNode;
                return;
            }
            else
            if (currConsumerNode != Volatile.Read(ref producerNode))
            {
                while ((nextNode = currConsumerNode.LvNext()) != null) ;

                nextNode.Free();
                consumerNode = nextNode;
                return;
            }
        }

        internal bool Peek(out T value)
        {
            Node currConsumerNode = consumerNode;

            Node nextNode = currConsumerNode.LvNext();

            if (nextNode != null)
            {
                value = nextNode.LpValue();
                return true;
            }
            if (currConsumerNode != Volatile.Read(ref producerNode))
            {
                while ((nextNode = currConsumerNode.LvNext()) != null) ;

                value = nextNode.LpValue();
                return true;
            }

            value = default(T);
            return false;
        }



        internal bool IsEmpty()
        {
            return Volatile.Read(ref producerNode) == Volatile.Read(ref consumerNode);
        }

        internal void Clear()
        {
            for (;;)
            {
                Node n = Volatile.Read(ref producerNode);

                n.Free();

                consumerNode = n;

                if (n == Volatile.Read(ref producerNode))
                {
                    break;
                }
            }
        }

        internal int Size()
        {
            Node chaserNode = Volatile.Read(ref consumerNode);
            Node prodNode = Volatile.Read(ref producerNode);

            int c = 0;

            while (chaserNode != prodNode && c < int.MaxValue)
            {
                Node next;

                while ((next = chaserNode.LvNext()) == null) ;

                chaserNode = next;

                c++;
            }

            return c;
        }

        internal Node CreateNode()
        {
            return new Node();
        }

        internal class Node
        {
            internal T value;

            Node next;

            public Node()
            {

            }

            public Node(T value)
            {
                this.value = value;
            }

            public void SoNext(Node n)
            {
                Volatile.Write(ref next, n);
            }

            public Node LvNext()
            {
                return Volatile.Read(ref next);
            }

            public Node LpNext()
            {
                return next;
            }

            public T LpValue()
            {
                return value;
            }

            public T LpValueAndFree()
            {
                T v = value;
                value = default(T);
                return v;
            }

            public void Free()
            {
                value = default(T);
            }
        }
    }
}
