using System;
using System.Collections.Generic;

namespace Iris.ContentManagement.Editor
{
    public struct PackageBuildState
    {
        public Utility.SIndex collection;

        // 不需要使用名字 guid 即可
        public string name;

        // 不允许新增资源
        public bool frozen;
    }

    public struct CollectionBuildState
    {
        public string name;
        public string description;
        public string extension;
    }
}
