using UnityEngine;
using YG;

namespace Vodovorot.RoundExperience
{
    /// <summary>References existing map objects; all scenery and labels are authored assets.</summary>
    public sealed class RoundTargetGroup : MonoBehaviour
    {
        [SerializeField] private string nameRu;
        [SerializeField] private string nameEn;
        [SerializeField] private string targetKindRu;
        [SerializeField] private string targetKindEn;
        [SerializeField] private FallingObject[] targets;
        [SerializeField] private TextMesh districtLabel;
        private bool russian;
        public FallingObject[] Targets => targets;
        public string DisplayName(bool ru) => ru ? nameRu : nameEn;
        public string TargetKind(bool ru) => ru ? targetKindRu : targetKindEn;

        private void OnEnable() => RefreshLabel();
        private void Update()
        {
            if (russian != (YG2.saves == null || YG2.saves.langRu))
                RefreshLabel();
        }

        private void RefreshLabel()
        {
            russian = YG2.saves == null || YG2.saves.langRu;
            if (districtLabel != null)
                districtLabel.text = DisplayName(russian);
        }
    }
}
