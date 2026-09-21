using UnityEngine;

namespace Natori.CityBuilder.Editor
{
    //Handles.Discのドラッグ角と、モデルへ適用済みの角を分離する。
    //範囲外で拒否された角は確定せず、次のドラッグ入力でも現在のモデルから差分を計算する。
    internal sealed class NatoriCityRotationDrag
    {
        private Quaternion _handleRotation = Quaternion.identity;
        private int _appliedQuarterTurns;

        public Quaternion HandleRotation => _handleRotation;

        public void Reset()
        {
            _handleRotation = Quaternion.identity;
            _appliedQuarterTurns = 0;
        }

        public int Update(Quaternion rotation)
        {
            _handleRotation = rotation;
            float angle = Vector3.SignedAngle(Vector3.forward, rotation * Vector3.forward, Vector3.up);
            int targetQuarterTurns = Mathf.RoundToInt(angle / 90.0f);
            //180度から-180度への境界をまたいでも、モデルへ二重に一周分を適用しない。
            return Mathf.RoundToInt(Mathf.DeltaAngle(_appliedQuarterTurns * 90.0f, targetQuarterTurns * 90.0f) / 90.0f);
        }

        public void Accept(int quarterTurns)
        {
            _appliedQuarterTurns = ((_appliedQuarterTurns + quarterTurns) % 4 + 4) % 4;
        }
    }
}
