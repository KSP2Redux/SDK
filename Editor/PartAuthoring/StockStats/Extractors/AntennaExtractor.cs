#if REDUX
using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats.Extractors
{
    /// <summary>Communication range (m) and data rate (packet/interval) from Data_Transmitter.</summary>
    internal sealed class AntennaExtractor : IStockFieldExtractor
    {
        /// <inheritdoc />
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            TransmitterDataObjectMirror t = ModuleResolver.FindModuleData<TransmitterDataObjectMirror>(part);
            if (t == null)
            {
                yield break;
            }
            if (t.CommunicationRange > 0.0)
            {
                yield return (StockFieldNames.AntennaRange, (float)t.CommunicationRange);
            }
            if (t.DataTransmissionInterval > 0f && t.DataPacketSize > 0f)
            {
                yield return (StockFieldNames.AntennaDataRate, t.DataPacketSize / t.DataTransmissionInterval);
            }
        }
    }
}
#endif
