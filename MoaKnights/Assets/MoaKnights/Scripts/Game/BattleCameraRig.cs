using UnityEngine;

namespace MoaKnights.Game
{
    /// <summary>
    /// 전투 카메라. 맵 전체가 카드 줄 위 영역에 들어오게 맞춘다.
    /// 원근(기본): 정면에서 내려다보며, 거리를 찾고 맵이 남은 영역 가운데 오게 옆으로 옮긴다. 직교: 예전 쿼터뷰.
    /// </summary>
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
            _fittedAspect = _camera.aspect;
            if (_view.CameraPerspective) FitPerspective();
            else FitOrthographic();
        }

        private void FitPerspective()
        {
            var rotation = Quaternion.Euler(_view.CameraPitch, _view.CameraYaw, 0f);
            var forward = rotation * Vector3.forward;
            var reserve = Mathf.Clamp(_view.CameraBottomReserve, 0f, 0.9f);
            var top = Mathf.Clamp(_view.CameraTopReserve, 0f, 0.9f - reserve);
            var bounds = _target;
            bounds.Expand(_view.CameraPadding * 2f);

            _camera.orthographic = false;
            _camera.fieldOfView = _view.CameraFieldOfView;
            transform.rotation = rotation;

            // 맵이 들어가는 가장 가까운 거리를 찾고, 화면에서 맵 가운데가 카드 줄 위 영역 가운데 오게 옮기기를 몇 번 되풀이한다.
            var focus = bounds.center;
            var distance = 0f;
            for (var pass = 0; pass < 4; pass++)
            {
                var near = 0.5f;
                var far = 500f;
                for (var i = 0; i < 40; i++)
                {
                    var mid = (near + far) * 0.5f;
                    transform.position = focus - forward * mid;
                    if (Fits(bounds, reserve, top)) far = mid;
                    else near = mid;
                }
                distance = far;
                transform.position = focus - forward * distance;

                ViewportBox(bounds, out var min, out var max);
                var current = (min + max) * 0.5f;
                var wanted = new Vector2(0.5f, reserve + (1f - top - reserve) * 0.5f);
                var depth = Vector3.Dot(bounds.center - transform.position, forward);
                var shift = _camera.ViewportToWorldPoint(new Vector3(current.x, current.y, depth))
                    - _camera.ViewportToWorldPoint(new Vector3(wanted.x, wanted.y, depth));
                focus += shift;
            }
            transform.position = focus - forward * distance;
        }

        private bool Fits(Bounds bounds, float reserve, float top)
        {
            ViewportBox(bounds, out var min, out var max);
            return min.x >= 0f && max.x <= 1f && min.y >= reserve && max.y <= 1f - top;
        }

        /// <summary>상자 여덟 모서리가 화면(뷰포트 0~1)에서 차지하는 범위. 카메라 뒤에 있는 점이 있으면 화면 밖으로 친다.</summary>
        private void ViewportBox(Bounds bounds, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            for (var sx = -1; sx <= 1; sx += 2)
            for (var sy = -1; sy <= 1; sy += 2)
            for (var sz = -1; sz <= 1; sz += 2)
            {
                var point = _camera.WorldToViewportPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(sx, sy, sz)));
                if (point.z <= 0f) point = new Vector3(-10f, -10f, 0f);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
        }

        private void FitOrthographic()
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
            _camera.orthographic = true;
            _camera.orthographicSize = Mathf.Max(
                (halfHeight + _view.CameraPadding) / (1f - reserve),
                halfWidth / _fittedAspect + _view.CameraPadding);

            var center = _target.center - up * (reserve * _camera.orthographicSize);
            transform.SetPositionAndRotation(center - rotation * Vector3.forward * _view.CameraDistance, rotation);
        }
    }
}
