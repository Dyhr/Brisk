using System;

namespace Brisk.Actions
{
    [AttributeUsage(AttributeTargets.Method)]
    public class Action : Attribute
    {
        public bool Self { get; }

        public Action(bool self = true)
        {
            Self = self;
        }
    }
}