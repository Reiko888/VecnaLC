using Dusk;
using UnityEngine;

namespace Vecna
{
    internal class VecnaContentHandler : ContentHandler<VecnaContentHandler>
    {
        internal VecnaAssets? vecnaAssets;

        public class VecnaAssets(DuskMod mod, string filePath) : AssetBundleLoader<VecnaAssets>(mod, filePath)
        {
            [LoadFromBundle("Vecna.prefab")]
            public GameObject Vecna { get; private set; } = null!;
        }


        public VecnaContentHandler(DuskMod mod) : base(mod)
        {
            RegisterContent("vecnabundle", out vecnaAssets);
        }
    }
}


