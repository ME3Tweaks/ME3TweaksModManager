/* Copyright (c) 2012 Rick (rick 'at' gibbed 'dot' us)
 * 
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 * 
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 * 
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.Collections;

namespace ME3TweaksModManager.modmanager.save.game3
{
    public class BitArrayWrapper : IList
    {
        public readonly BitArray Target;

        // for CollectionEditor, so it knows the correct Item type
        // ReSharper disable UnusedAutoPropertyAccessor.Local
        public bool Item { get; private set; }
        // ReSharper restore UnusedAutoPropertyAccessor.Local

        public BitArrayWrapper(BitArray target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target", "target cannot be null");
            }

            this.Target = target;
        }

        #region IEnumerable Members
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.Target.GetEnumerator();
        }
        #endregion

        #region IList Members
        int IList.Add(object value)
        {
            if ((value is bool) == false)
            {
                throw new ArgumentException("value");
            }

            var index = this.Target.Length;
            this.Target.Length++;
            this.Target[index] = (bool)value;
            return index;
        }

        void IList.Clear()
        {
            this.Target.Length = 0;
        }

        bool IList.Contains(object value)
        {
            throw new NotSupportedException();
        }

        int IList.IndexOf(object value)
        {
            throw new NotSupportedException();
        }

        void IList.Insert(int index, object value)
        {
            if ((value is bool) == false)
            {
                throw new ArgumentException("value");
            }

            if (index >= this.Target.Length)
            {
                this.Target.Length = index + 1;
                this.Target[index] = (bool)value;
            }
            else
            {
                this.Target.Length++;
                for (int i = this.Target.Length - 1; i > index; i--)
                {
                    this.Target[i] = this.Target[i - 1];
                }
                this.Target[index] = (bool)value;
            }
        }

        bool IList.IsFixedSize
        {
            get { return false; }
        }

        bool IList.IsReadOnly
        {
            get { return false; }
        }

        void IList.Remove(object value)
        {
            throw new NotSupportedException();
        }

        void IList.RemoveAt(int index)
        {
            if (index >= this.Target.Length)
            {
                throw new IndexOutOfRangeException();
            }

            for (int i = this.Target.Length - 1; i > index; i--)
            {
                this.Target[i - 1] = this.Target[i];
            }
            this.Target.Length--;
        }

        object IList.this[int index]
        {
            get { return this.Target[index]; }
            set
            {
                if ((value is bool) == false)
                {
                    throw new ArgumentException("value");
                }

                this.Target[index] = (bool)value;
            }
        }
        #endregion

        #region ICollection Members
        void ICollection.CopyTo(Array array, int index)
        {
            for (int i = 0; i < this.Target.Length; i++)
            {
                array.SetValue(this.Target[i], index + i);
            }
        }

        int ICollection.Count
        {
            get { return this.Target.Length; }
        }

        bool ICollection.IsSynchronized
        {
            get { return false; }
        }

        object ICollection.SyncRoot
        {
            get { return this; }
        }
        #endregion
    }
}
