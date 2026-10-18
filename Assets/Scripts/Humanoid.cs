using UnityEngine;

// Builds a unit's visual. Humanoids are built from a three-way sprite set
// (facing the camera / back of the head / side profile) and are never spun
// around: the renderer swaps views and flips for left, Pokemon-style. Plain
// top-down props (mechs, the spider, the grabber) still use the single
// rotating sprite.
public static class Humanoid
{
    public static Transform Build(Transform parent, Art.UnitArt art, float scale = 1f, int order = 20,
                                  AnimStyle style = AnimStyle.Standard)
    {
        var root = Root(parent, art.down, scale, order);

        var anim = root.gameObject.AddComponent<HumanoidAnim>();
        anim.style = style;
        anim.faceDown = art.down;
        anim.faceUp = art.up;
        anim.Capture();

        // the weapon rides the body (as its child, so lunges and leans carry
        // it) but keeps swinging to point at the aim
        if (art.gun != null)
        {
            var g = new GameObject("Gun");
            g.transform.SetParent(root.GetChild(0), false);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = art.gun;
            sr.sortingOrder = order + 1;              // over the body
            anim.gun = g.transform;
            anim.gunSr = sr;
            anim.gunAx = art.gunAx;
            anim.gunAy = art.gunAy;
            anim.SetAim(Vector2.right);
        }
        return root;
    }

    // single sprite, spins to face the aim/move direction (mechs and monsters)
    public static Transform Build(Transform parent, Sprite sprite, float scale = 1f, int order = 20,
                                  AnimStyle style = AnimStyle.Standard)
    {
        var root = Root(parent, sprite, scale, order);

        var anim = root.gameObject.AddComponent<HumanoidAnim>();
        anim.style = style;
        anim.Capture();
        return root;
    }

    static Transform Root(Transform parent, Sprite sprite, float scale, int order)
    {
        var root = new GameObject("Visual").transform;
        root.SetParent(parent, false);
        root.localScale = Vector3.one * scale;

        var g = new GameObject("Body");
        g.transform.SetParent(root, false);
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return root;
    }

    // recoil for firing / lunging
    public static void Kick(Transform visual, float power)
    {
        if (visual == null) return;
        var a = visual.GetComponent<HumanoidAnim>();
        if (a != null) a.Kick(power);
    }
}
