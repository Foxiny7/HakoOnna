using HakoOnna.Rules;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("HakoOnna.Tests.EditMode")]

namespace HakoOnna.Multiplayer
{
    public sealed partial class NetworkPlayer
    {
        private NetworkList<int> slots;
        private readonly NetworkVariable<int> selected = new(0, NetworkVariableReadPermission.Owner);
        private readonly NetworkVariable<bool> lampOn = new();
        private readonly NetworkVariable<Vector2> lampAngles = new();
        private Transform itemAnchor, gripPart, rightArm;
        private Vector3 gripLocal, restArmPosition;
        private Quaternion restArmRotation;
        private float approach, openTime;
        private Transform lampModel;
        private Light beam;

        private void Awake() => slots = new NetworkList<int>(null, NetworkVariableReadPermission.Owner);

        private static Transform Primitive(PrimitiveType type, string name, Transform parent, Vector3 scale)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            var collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            part.transform.SetParent(parent, false);
            part.transform.localScale = scale;
            return part.transform;
        }

        private void InitializeEquipment()
        {
            rightArm = arms.transform.Find("RightArm");
            restArmPosition = rightArm.localPosition;
            restArmRotation = rightArm.localRotation;
            var palm = Primitive(PrimitiveType.Sphere, "Hand", rightArm, new Vector3(1.18f, .33f, .92f));
            palm.localPosition = Vector3.up * .93f;
            lampModel = new GameObject("Flashlight").transform;
            lampModel.SetParent(transform, false);
            var barrel = Primitive(PrimitiveType.Cylinder, "Barrel", lampModel, new Vector3(.047f, .075f, .047f));
            barrel.localRotation = Quaternion.Euler(90, 0, 0);
            var finish = new MaterialPropertyBlock();
            finish.SetColor("_BaseColor", new Color(.12f, .14f, .16f));
            barrel.GetComponent<Renderer>().SetPropertyBlock(finish);
            var lens = Primitive(PrimitiveType.Cylinder, "Lens", lampModel, new Vector3(.065f, .018f, .065f));
            lens.localRotation = barrel.localRotation;
            lens.localPosition = Vector3.forward * .08f;
            var emitter = new GameObject("Beam").transform;
            emitter.SetParent(lampModel, false);
            emitter.localPosition = Vector3.forward * .11f;
            beam = emitter.gameObject.AddComponent<Light>();
            beam.type = LightType.Spot;
            beam.range = 16;
            beam.spotAngle = 55;
            beam.innerSpotAngle = 25;
            beam.intensity = 5;
            beam.color = new Color(1f, .95f, .84f);
            beam.shadows = LightShadows.Soft;
            beam.enabled = false;
        }

        private void ReadEquipmentKeys(Keyboard keyboard)
        {
            for (var i = 0; i < MatchRules.InventorySize; i++)
                if (keyboard[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                    SelectSlotRpc(i, false);
            var scroll = Mouse.current?.scroll.ReadValue().y ?? 0;
            if (scroll != 0)
                SelectSlotRpc(scroll > 0 ? -1 : 1, true);
            if (keyboard.tKey.wasPressedThisFrame)
                ToggleLampRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SelectSlotRpc(int value, bool relative)
        {
            int size = MatchRules.InventorySize;
            int slot = relative ? (selected.Value + Mathf.Clamp(value, -1, 1) + size) % size : value;
            if (slot < 0 || slot >= size || slot == selected.Value) return;
            selected.Value = slot;
            heldItem.Value = slots[slot];
            lampOn.Value = false;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void ToggleLampRpc()
        {
            if (heldItem.Value == (int)ItemKind.Flashlight)
                lampOn.Value = !lampOn.Value;
        }

        private void UpdateEquipmentPose()
        {
            bool equipped = IsOwner ? heldItem.Value == (int)ItemKind.Flashlight : lampOn.Value;
            lampModel.gameObject.SetActive(equipped);
            if (!equipped) return;
            var rotation = IsOwner ? playerCamera.transform.rotation :
                Quaternion.Euler(lampAngles.Value.x, lampAngles.Value.y, 0);
            var eye = IsOwner ? playerCamera.transform.position : transform.position + Vector3.up * config.EyeHeight;
            lampModel.SetPositionAndRotation(eye + rotation * new Vector3(-.17f, -.19f, .36f), rotation);
            beam.enabled = lampOn.Value;
        }

        private void PrepareInspection(Transform visual)
        {
            var root = visual.parent;
            var drawer = visual.Find("Drawer_2/ItemAnchor")?.parent;
            var grip = drawer ? drawer.Find("Handle") : root.Find("GrabAnchor");
            gripPart = drawer ?? visual.Find("Lid_Pivot") ?? visual.Find("Door_R_Pivot")
                ?? visual.Find("Door_Pivot") ?? visual.Find("Fusuma_R") ?? grip;
            gripLocal = gripPart.InverseTransformPoint(grip.position);
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == "ItemAnchor") { itemAnchor = child; break; }
            openTime = 0;
        }

        private void UpdateInspectionPose()
        {
            var camera = playerCamera.transform;
            var eye = transform.position + Vector3.up * config.EyeHeight;
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            approach = Mathf.MoveTowards(approach, requested >= 0 ? 1 : 0, Time.deltaTime * 3.5f);
            float weight = Mathf.SmoothStep(0, 1, approach);
            if (weight <= 0 || !gripPart)
            {
                camera.SetPositionAndRotation(eye, rotation);
                rightArm.localPosition = restArmPosition;
                rightArm.localRotation = restArmRotation;
                return;
            }
            if (Observing) openTime += Time.deltaTime;
            var grip = gripPart.TransformPoint(gripLocal);
            var focus = Vector3.Lerp(grip, itemAnchor.position + Vector3.up * .08f, Mathf.Clamp01(openTime * 2));
            camera.position = Vector3.Lerp(eye, InspectionPosition(eye, focus, itemAnchor, gripPart,
                Mathf.Clamp01(openTime * 2), gameObject.scene.GetPhysicsScene()), weight);
            var look = Quaternion.LookRotation(focus - camera.position);
            if (Observing && Mouse.current != null)
            {
                var pointer = Mouse.current.position.ReadValue();
                float x = Mathf.Clamp(pointer.x / Screen.width * 2 - 1, -1, 1);
                float y = Mathf.Clamp(pointer.y / Screen.height * 2 - 1, -1, 1);
                look *= Quaternion.Euler(-10 * y, 15 * x, 0);
            }
            camera.rotation = Quaternion.Slerp(rotation, look, weight);
            var rest = rightArm.parent.TransformPoint(restArmPosition);
            if (requested >= 0 && duration == 0)
            {
                // Let the arms swing on the approach, then reach once the feet are planted.
                float stride = Mathf.Sin(Time.time * 11) * .035f;
                rightArm.position = rest + camera.forward * stride;
                rightArm.localRotation = restArmRotation;
                return;
            }
            var shoulder = camera.TransformPoint(new Vector3(.19f, -.26f, .08f));
            var reach = Vector3.ClampMagnitude(grip - shoulder, .83f);
            float handWeight = weight * (1 - Mathf.SmoothStep(0, .85f, Mathf.Clamp01((openTime - .35f) * 3)));
            rightArm.position = Vector3.Lerp(rest, shoulder + reach - reach.normalized * .15f, handWeight);
            rightArm.rotation = Quaternion.Slerp(rightArm.parent.rotation * restArmRotation,
                Quaternion.FromToRotation(Vector3.up, reach.normalized), handWeight);
        }

        internal static Vector3 InspectionPosition(Vector3 eye, Vector3 focus, Transform anchor, Transform movingPart,
            float opened, PhysicsScene physics)
        {
            var offset = focus - eye;
            var destination = eye + offset.normalized * Mathf.Clamp(offset.magnitude - .5f, 0, .48f);
            bool drawer = anchor.parent == movingPart;
            if (drawer || movingPart.name == "Lid_Pivot")
            {
                // Look over the front edge as the tray extends or the lid lifts.
                var cabinet = movingPart.parent;
                var above = anchor.position + cabinet.forward * .17f;
                above.y = drawer ? Mathf.Max(eye.y - .25f, anchor.position.y + .72f)
                    : Mathf.Max(eye.y, movingPart.position.y + .45f);
                destination = Vector3.Lerp(destination, above, Mathf.SmoothStep(0, 1, opened));
            }
            var direction = (destination - eye).normalized;
            float lean = Vector3.Distance(eye, destination);
            if (physics.SphereCast(eye, .10f, direction, out var hit, lean,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                lean = Mathf.Max(0, hit.distance - .03f);
            return eye + direction * lean;
        }

        private void DrawHotbar()
        {
            float width = Mathf.Min(78, (Screen.width - 12f) / MatchRules.InventorySize);
            float left = (Screen.width - width * MatchRules.InventorySize) / 2;
            for (int i = 0; i < slots.Count; i++)
            {
                var name = slots[i] < 0 ? "—" : ((ItemKind)slots[i]).ToString();
                var color = GUI.backgroundColor;
                if (i == selected.Value) GUI.backgroundColor = Color.yellow;
                GUI.Box(new Rect(left + i * width, Screen.height - 48, width - 2, 43), (i + 1) + "\n" + name);
                GUI.backgroundColor = color;
            }
        }
    }
}
