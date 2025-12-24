using Verse;

namespace ProductionFlow
{
    public class ProductionFlowMod : Mod
    {
        private static ProductionFlowMod _instance;
        public static ProductionFlowMod Instance
        {
            get { return _instance; }
        }

        public ProductionFlowMod(ModContentPack content) : base(content)
        {
            _instance = this;
        }
    }
}

