using System;

namespace Iris.ContentManagement
{
    public class DirectoryNotFoundException : Exception
    {
        private string _name;

        public DirectoryNotFoundException(string name)
        {
            _name = name;
        }

        public override string ToString()
        {
            return _name;
        }
    }
}