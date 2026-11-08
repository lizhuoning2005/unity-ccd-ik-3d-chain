using System.Collections.Generic;
using UnityEngine;

public class CCD_IK_Chain3D : MonoBehaviour
{
    [Header("关节参数")]
    [Tooltip("关节总数，至少2")]
    public int jointCount = 4;
    [Tooltip("每段骨骼长度")]
    public float boneLength = 1f;
    [Tooltip("CCD迭代次数，越高越准")]
    public int iterations = 20;
    [Tooltip("距离阈值，足够近就停止迭代")]
    public float threshold = 0.001f;
    [Tooltip("限制单步旋转角度，防止关节瞬间翻转")]
    public float maxAngleStep = 30f;

    [Header("拖拽设置")]
    public bool dragEndEffector = true;

    // 关节列表：[0]根节点，最后一个是末端执行器
    public List<Transform> joints { get; private set; }
    private Transform root;
    private Transform endEffector;
    private Vector3 targetPos;
    private bool isDragging;

    void Start()
    {
        GenerateChain();
    }

    [ContextMenu("重新生成关节链")]
    void GenerateChain()
    {
        // 销毁旧关节
        if (joints != null)
        {
            foreach (var j in joints)
            {
                if (j != null) DestroyImmediate(j.gameObject);
            }
        }
        joints = new List<Transform>();

        if (jointCount < 2) jointCount = 2;

        // 创建根节点
        root = new GameObject("RootJoint").transform;
        root.SetParent(transform);
        root.localPosition = Vector3.zero;
        joints.Add(root);

        Transform last = root;
        // 沿着X轴生成整条链
        for (int i = 1; i < jointCount; i++)
        {
            Transform joint = new GameObject($"Joint_{i}").transform;
            joint.SetParent(last);
            joint.localPosition = new Vector3(boneLength, 0, 0);
            joints.Add(joint);
            last = joint;
        }
        endEffector = last;
        targetPos = endEffector.position;
    }

    void Update()
    {
        if (dragEndEffector)
        {
            HandleDrag();
        }
        SolveCCD(targetPos);
    }

    // 3D鼠标拖拽：射线拾取末端小球
    void HandleDrag()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Input.GetMouseButtonDown(0))
        {
            // 简单距离检测，也可以换成Collider拾取
            float dist = Vector3.Distance(ray.GetPoint(10), endEffector.position);
            if (dist < 0.6f)
            {
                isDragging = true;
            }
        }
        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }

        if (isDragging)
        {
            // 取射线在距离相机10米处的点作为目标
            targetPos = ray.GetPoint(10f);
        }
    }

    void SolveCCD(Vector3 target)
    {
        for (int it = 0; it < iterations; it++)
        {
            // 从倒数第二个关节，向根节点遍历
            for (int i = joints.Count - 2; i >= 0; i--)
            {
                Transform currJoint = joints[i];
                Vector3 dirToEnd = endEffector.position - currJoint.position;
                Vector3 dirToTarget = target - currJoint.position;

                // 方向向量归一化
                dirToEnd.Normalize();
                dirToTarget.Normalize();

                // 计算从当前末端方向转向目标方向的旋转
                Quaternion rotDelta = Quaternion.FromToRotation(dirToEnd, dirToTarget);

                // 限制旋转幅度，防止大幅度翻转抖动
                rotDelta = Quaternion.RotateTowards(Quaternion.identity, rotDelta, maxAngleStep);

                currJoint.rotation = rotDelta * currJoint.rotation;

                // 已经足够靠近目标，提前退出
                if (Vector3.Distance(endEffector.position, target) < threshold)
                    return;
            }
        }
    }

    void OnDrawGizmos()
    {
        if (joints == null || joints.Count < 2) return;

        // 绘制骨骼连线
        Gizmos.color = Color.green;
        for (int i = 0; i < joints.Count - 1; i++)
        {
            Gizmos.DrawLine(joints[i].position, joints[i + 1].position);
            Gizmos.DrawSphere(joints[i].position, 0.08f);
        }

        // 末端执行器（红色）
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(endEffector.position, 0.12f);

        // IK目标位置（黄色线框球）
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(targetPos, 0.15f);
    }
}
