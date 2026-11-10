using UnityEngine;

namespace MoaKnights.Game
{
    /// <summary>쿼터뷰 직교 카메라. 맵 전체가 화면에 들어오게 맞춘다.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class BattleCameraRig : MonoBehaviour
    {
        private Camera _camera;
        private BattleViewAsset _view;
        private Bounds _target;
        private float _fittedAspect;

        public void Frame(Bounds target, BattleViewAsset view)
        {
            _camera = GetComponent<Camera>();
            _target = target;
            _view = view;
            Fit();
        }

        private void LateUpdate()
        {
            if (_camera != null && !Mathf.Approximately(_camera.aspect, _fittedAspect)) Fit();
        }

        private void Fit()
        {
            var rotation = Quaternion.Euler(_view.CameraPitch, _view.CameraYaw, 0f);
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            var halfWidth = 0f;
            var halfHeight = 0f;
            for (var sx = -1; sx <= 1; sx += 2)
            for (var sy = -1; sy <= 1; sy += 2)
            for (var sz = -1; sz <= 1; sz += 2)
            {
                var corner = Vector3.Scale(_target.extents, new Vector3(sx, sy, sz));
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector3.Dot(corner, right)));
                halfHeight = Mathf.Max(halfHeight, Mathf.Abs(Vector3.Dot(corner, up)));
            }

            // 아래쪽 reserve 비율은 카드 줄 자리다. 남은 위쪽에 맵이 들어가게 크기를 키우고 그만큼 카메라를 내린다.
            var reserve = Mathf.Clamp(_view.CameraBottomReserve, 0f, 0.9f);
            _fittedAspect = _camera.aspect;
            _camera.orthographic = true;
            _camera.orthographicSize = Mathf.Max(
                (halfHeight + _view.CameraPadding) / (1f - reserve),
                halfWidth / _fittedAspect + _view.CameraPadding);

            var center = _target.center - up * (reserve * _camera.orthographicSize);
            transform.SetPositionAndRotation(center - rotation * Vector3.forward * _view.CameraDistance, rotation);
        }
    }
}
