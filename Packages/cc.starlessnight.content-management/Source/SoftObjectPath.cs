using System;
using System.IO;

namespace Iris.ContentManagement
{
    using UnityEngine;

    [Serializable]
    public struct SoftObjectPath
    {
        [SerializeField]
        private string _assetGUID;

        [SerializeField]
        private string _subObjectName;

        [SerializeField]
        private string _subObjectType;

        public Object TryLoad()
        {
            throw new NotImplementedException();
        }
    }
}
