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

namespace MassEffectModManagerCore.modmanager.save.game3
{
    internal class LocalizedString
    {
        private Type _ResourceType;
        private string _PropertyName;
        private Func<string> _CachedValue;

        private void ResetCache()
        {
            this._CachedValue = null;
        }

        public Type ResourceType
        {
            get { return this._ResourceType; }
            set
            {
                if (this._ResourceType == value)
                {
                    return;
                }

                this.ResetCache();
                this._ResourceType = value;
            }
        }

        public string PropertyName
        {
            get { return this._PropertyName; }
            set
            {
                if (this._PropertyName == value)
                {
                    return;
                }

                this.ResetCache();
                this._PropertyName = value;
            }
        }

        public string GetLocalizedValue()
        {
            if (this._CachedValue != null)
            {
                return this._CachedValue();
            }

            if (this._ResourceType == null ||
                this._PropertyName == null)
            {
                this._CachedValue = () => null;
                return this._CachedValue();
            }

            if (this._ResourceType.IsVisible == false)
            {
                this._CachedValue =
                    () =>
                    {
                        throw new InvalidOperationException(string.Format("{0} is not visible",
                                                                          this._ResourceType.FullName));
                    };
                return this._CachedValue();
            }

            var property = this._ResourceType.GetProperty(this._PropertyName);
            if (property == null)
            {
                /*
                this._CachedValue =
                    () =>
                    {
                        throw new InvalidOperationException(string.Format("{0} does not have a public property {1}",
                                                                          this._ResourceType.FullName,
                                                                          this._PropertyName));
                    };
                */
                this._CachedValue = () => null;
                return this._CachedValue();
            }

            if (property.PropertyType != typeof(string))
            {
                this._CachedValue =
                    () =>
                    {
                        throw new InvalidOperationException(string.Format("{0} {1} is not a string",
                                                                          this._ResourceType.FullName,
                                                                          property.Name));
                    };
                return this._CachedValue();
            }

            var getMethod = property.GetGetMethod();
            if (getMethod == null ||
                getMethod.IsPublic == false ||
                getMethod.IsStatic == false)
            {
                this._CachedValue =
                    () =>
                    {
                        throw new InvalidOperationException(string.Format("{0} {1} getter is not public",
                                                                          this._ResourceType.FullName,
                                                                          property.Name));
                    };
                return this._CachedValue();
            }

            this._CachedValue = () => (string)property.GetValue(null, null);
            return this._CachedValue();
        }
    }
}
