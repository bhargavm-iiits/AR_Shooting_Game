using UnityEngine;

namespace RealityDefender
{
    /// <summary>Camera-relative presentation only. This transform never supplies the physics shot pose.</summary>
    public sealed class RailgunPresentation : MonoBehaviour
    {
        public Vector3 landscapeOffset = new Vector3(0f, -.15f, .32f);
        public Vector3 portraitOffset = new Vector3(0f, -.18f, .38f);
        float centerCorrectionX;
        Camera cameraOwner;
        Vector3 restPosition;
        Quaternion restRotation;
        float recoil;
        bool visible = true;
        Renderer[] visuals;

        public void Bind(Camera camera)
        {
            cameraOwner = camera;
            restRotation = transform.localRotation;
            visuals = GetComponentsInChildren<Renderer>();
            // Viewmodels are render-only even if the input asset accidentally contains physics components.
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (var body in GetComponentsInChildren<Rigidbody>()) { body.isKinematic = true; body.detectCollisions = false; }
            // Centre the visible gun on the screen's vertical axis, whatever the mesh pivot is.
            centerCorrectionX = 0f;
            bool any = false;
            Bounds local = default;
            foreach (var renderer in visuals)
            {
                if (!(renderer is MeshRenderer) || !renderer.TryGetComponent(out MeshFilter filter) || !filter.sharedMesh) continue;
                Bounds mesh = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = transform.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if (!any) { local = new Bounds(p, Vector3.zero); any = true; } else local.Encapsulate(p);
                }
            }
            if (any) centerCorrectionX = -local.center.x;
            // Muzzle: the support point of the mesh bounds along the camera's forward axis (works for any mesh orientation).
            if (any && cameraOwner)
            {
                Vector3 forwardLocal = transform.InverseTransformDirection(cameraOwner.transform.forward).normalized;
                Vector3 e = local.extents;
                float reach = Mathf.Abs(forwardLocal.x) * e.x + Mathf.Abs(forwardLocal.y) * e.y + Mathf.Abs(forwardLocal.z) * e.z;
                muzzleLocal = local.center + forwardLocal * reach;
                hasMuzzle = true;
            }
            RefreshOffset();
        }

        Vector3 muzzleLocal;
        bool hasMuzzle;

        /// <summary>World position of the barrel tip, including recoil. Presentation only; shots still fly from the camera.</summary>
        public Vector3 MuzzleWorld => hasMuzzle ? transform.TransformPoint(muzzleLocal)
            : cameraOwner ? cameraOwner.transform.TransformPoint(new Vector3(0f, -0.1f, 0.35f)) : transform.position;

        public void Recoil() => recoil = 1f;
        public void SetVisible(bool value)
        {
            visible = value;
            if (visuals == null) return;
            foreach (var renderer in visuals) if (renderer) renderer.enabled = value;
        }

        void LateUpdate()
        {
            if (!cameraOwner) return;
            RefreshOffset();
            recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * 7f);
            transform.localPosition = restPosition + new Vector3(0f, -.009f, -.025f) * recoil;
            transform.localRotation = restRotation * Quaternion.Euler(-4f * recoil, 0f, 0f);
        }

        void RefreshOffset()
        {
            bool portrait = cameraOwner && cameraOwner.aspect < 1f;
            restPosition = portrait ? portraitOffset : landscapeOffset;
            restPosition.x += centerCorrectionX;
            restPosition.z = Mathf.Max(restPosition.z, cameraOwner.nearClipPlane + .12f);
            transform.localPosition = restPosition;
        }
    }
}
