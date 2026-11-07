using UnityEngine;

public sealed partial class EcoCatalogueToolVisual
{
    private void Vacuum()
    {
        var barrel = new Vector3(0, .055f, 0);
        var hull = new EcoToolMesh();
        hull.Lathe(shell, barrel, Forward, 16,
            new Vector2(0, -.194f), new Vector2(.054f, -.194f), new Vector2(.080f, -.167f),
            new Vector2(.089f, -.126f), new Vector2(.089f, .052f), new Vector2(.084f, .112f),
            new Vector2(.099f, .170f), new Vector2(.107f, .194f), new Vector2(.090f, .205f),
            new Vector2(.075f, .189f), new Vector2(.068f, .123f), new Vector2(0, .123f));
        hull.Ring(edge, barrel + Forward * .200f, Forward, .087f, .109f, .025f, 18);
        hull.Ring(rubber, barrel + Forward * .178f, Forward, .071f, .099f, .017f, 18);
        hull.Ring(metal, barrel - Forward * .156f, Forward, .067f, .081f, .022f, 16);
        hull.Ring(rubber, barrel + Forward * .098f, Forward, .073f, .091f, .013f, 16);
        hull.Cylinder(rubber, barrel + Forward * .134f, Forward, .077f, .017f, 16);
        for (int i = -2; i <= 2; i++)
        {
            float offset = i * .022f;
            float width = Mathf.Sqrt(.078f * .078f - offset * offset) * 2f;
            hull.Box(rubber, barrel + new Vector3(0f, offset, .168f), new Vector3(width, .0045f, .008f), .0014f);
            hull.Box(rubber, barrel + new Vector3(offset, 0f, .168f), new Vector3(.0045f, width, .008f), .0014f);
        }

        // Broad top carry loop, sloping rear grip and cylindrical filter cup from the catalogue silhouette.
        var front = new Vector3(0, .123f, .038f);
        var rear = new Vector3(0, .127f, -.153f);
        hull.Bar(edge, front, new Vector3(0, .185f, .009f), .041f, .025f, .009f);
        hull.Bar(edge, new Vector3(0, .185f, .009f), new Vector3(0, .188f, -.117f), .044f, .028f, .010f);
        hull.Bar(edge, new Vector3(0, .188f, -.117f), rear, .041f, .024f, .009f);
        hull.Bar(rubber, new Vector3(0, .188f, -.018f), new Vector3(0, .188f, -.099f), .048f, .030f, .010f);
        Grip(hull, new Vector3(0, .000f, -.117f), new Vector3(0, -.146f, -.173f), .061f);
        hull.Cylinder(rubber, new Vector3(0, -.076f, .035f), Up, .063f, .039f);
        hull.Cylinder(edge, new Vector3(0, -.152f, .035f), Up, .057f, .029f);
        hull.Cylinder(glass, new Vector3(0, -.119f, .035f), Up, .052f, .071f);
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.PI * .2f;
            var rib = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * .052f + new Vector3(0, -.119f, .035f);
            hull.Box(edge, rib, new Vector3(.008f, .061f, .010f), .002f, Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0));
        }
        foreach (float side in new[] { -1f, 1f })
        {
            var plate = new Vector3(side * .079f, .053f, -.074f);
            hull.Box(edge, plate, new Vector3(.018f, .067f, .095f), .011f);
            Leaf(hull, plate + new Vector3(side * .012f, 0, .006f), side);
            Vent(hull, new Vector3(side * .071f, .049f, -.149f), side, 3);
        }
        Finish(hull, "Motor gövdesi, filtre ve taşıma sapı");

        primary = Part("Emiş türbini", barrel + Forward * .151f);
        var rotor = new EcoToolMesh();
        rotor.Cylinder(metal, Vector3.zero, Forward, .020f, .020f);
        for (int i = 0; i < 6; i++)
        {
            var turn = Quaternion.AngleAxis(i * 60f + 10f, Forward);
            rotor.Box(edge, turn * new Vector3(.041f, .007f, 0f), new Vector3(.054f, .016f, .011f), .003f, turn);
        }
        Finish(rotor, "Fan kanatları", primary);
        secondary = Part("Basınç pistonu", new Vector3(0, -.076f, .036f));
        var piston = new EcoToolMesh();
        piston.Cylinder(metal, Vector3.zero, Up, .065f, .010f);
        Finish(piston, "Filtre bileziği", secondary);
        selector = Part("Üç konumlu seçici", new Vector3(0, .148f, -.035f));
        var dial = new EcoToolMesh();
        dial.Cylinder(rubber, Vector3.zero, Up, .030f, .017f);
        dial.Box(edge, new Vector3(0, .012f, .006f), new Vector3(.020f, .009f, .034f), .003f);
        Finish(dial, "Mod düğmesi", selector);
        var light = new EcoToolMesh();
        light.Box(signal, new Vector3(0, .147f, .046f), new Vector3(.041f, .012f, .025f), .005f);
        Lamp(light, "Mod ışığı");
        Anchors(new Vector3(0, -.054f, -.139f), barrel + Forward * .215f, new Vector3(-.069f, -.072f, .101f));
    }

    private void SeedLauncher()
    {
        var axis = new Vector3(0, .044f, 0);
        var hull = new EcoToolMesh();
        hull.Box(shell, new Vector3(0, .038f, -.022f), new Vector3(.124f, .102f, .224f), .017f);
        hull.Box(edge, new Vector3(0, .091f, -.001f), new Vector3(.101f, .016f, .126f), .006f);
        Grip(hull, new Vector3(0, -.003f, -.085f), new Vector3(0, -.146f, -.128f), .058f);
        hull.Bar(shell, new Vector3(0, .049f, -.123f), new Vector3(0, .049f, -.278f), .046f, .037f, .011f);
        hull.Bar(metal, new Vector3(0, -.019f, -.151f), new Vector3(0, -.064f, -.272f), .040f, .028f, .008f);
        hull.Bar(edge, new Vector3(0, -.064f, -.270f), new Vector3(0, .058f, -.277f), .061f, .021f, .007f);
        hull.Box(rubber, new Vector3(0, -.004f, -.289f), new Vector3(.072f, .145f, .020f), .008f);
        foreach (float side in new[] { -1f, 1f })
        {
            hull.Box(edge, new Vector3(side * .065f, .050f, -.014f), new Vector3(.014f, .047f, .115f), .009f);
            hull.Box(rubber, new Vector3(side * .074f, .050f, .025f), new Vector3(.009f, .019f, .041f), .003f);
            Bolt(hull, new Vector3(side * .074f, .051f, -.058f));
        }
        // Angled hopper above the breech, as on the field guide drawing.
        Vector3 hopperBase = new Vector3(0, .089f, .004f);
        Vector3 hopperAxis = new Vector3(0, .96f, -.28f).normalized;
        hull.Lathe(edge, hopperBase, hopperAxis, 12, new Vector2(.026f, 0), new Vector2(.050f, .039f),
            new Vector2(.052f, .054f), new Vector2(.050f, .103f));
        hull.Cylinder(glass, hopperBase + hopperAxis * .080f, hopperAxis, .047f, .046f, 12);
        hull.Ring(shell, hopperBase + hopperAxis * .102f, hopperAxis, .046f, .054f, .021f);
        for (int i = -1; i <= 1; i++)
            hull.Cylinder(accent, new Vector3(i * .021f, .161f, -.005f), Forward, .009f, .034f, 7);
        Finish(hull, "Kundak, kavrama ve tohum besleyici");

        primary = Part("Geri tepen namlu", axis);
        var tube = new EcoToolMesh();
        tube.Lathe(metal, Vector3.zero, Forward, 14, new Vector2(.037f, .044f), new Vector2(.044f, .056f),
            new Vector2(.045f, .178f), new Vector2(.050f, .200f), new Vector2(.049f, .220f),
            new Vector2(.032f, .220f), new Vector2(.029f, .197f), new Vector2(0, .189f));
        tube.Ring(edge, Forward * .130f, Forward, .042f, .061f, .044f);
        tube.Ring(rubber, Forward * .177f, Forward, .043f, .055f, .014f);
        tube.Ring(edge, Forward * .216f, Forward, .033f, .054f, .024f);
        Finish(tube, "Yivli besleme tüpü", primary);
        secondary = Part("Besleme mandalı", new Vector3(.076f, .052f, .009f));
        var latch = new EcoToolMesh();
        latch.Box(metal, new Vector3(.010f, 0, -.021f), new Vector3(.023f, .018f, .048f), .006f);
        Finish(latch, "Mandal", secondary);
        selector = Part("Tohum ve demir top seçicisi", new Vector3(.079f, .079f, -.074f));
        var dial = new EcoToolMesh();
        dial.Cylinder(rubber, Vector3.zero, Right, .023f, .015f, 10);
        dial.Box(accent, new Vector3(.013f, .010f, 0), new Vector3(.012f, .034f, .012f), .003f);
        Finish(dial, "Seçici kolu", selector);
        var light = new EcoToolMesh();
        light.Box(signal, new Vector3(0, .107f, .052f), new Vector3(.041f, .010f, .026f), .004f);
        Lamp(light, "Besleme ışığı");
        Anchors(new Vector3(0, -.052f, -.100f), Forward * .230f, new Vector3(-.050f, -.006f, .059f), primary);
    }

    private void Recycler()
    {
        var hull = new EcoToolMesh();
        hull.Box(shell, new Vector3(0, .050f, -.032f), new Vector3(.150f, .135f, .193f), .024f);
        hull.Box(rubber, new Vector3(0, .049f, -.140f), new Vector3(.143f, .128f, .060f), .015f);
        hull.Box(edge, new Vector3(0, .055f, .066f), new Vector3(.161f, .108f, .049f), .018f);
        hull.Bar(edge, new Vector3(0, .107f, -.126f), new Vector3(0, .169f, -.107f), .047f, .032f, .008f);
        hull.Bar(edge, new Vector3(0, .169f, -.107f), new Vector3(0, .168f, .010f), .054f, .036f, .009f);
        hull.Bar(edge, new Vector3(0, .168f, .010f), new Vector3(0, .109f, .042f), .047f, .032f, .008f);
        hull.Bar(rubber, new Vector3(0, .171f, -.090f), new Vector3(0, .171f, -.010f), .059f, .041f, .011f);
        hull.Box(rubber, new Vector3(-.051f, -.031f, .015f), new Vector3(.065f, .046f, .090f), .012f);
        foreach (float side in new[] { -1f, 1f })
        {
            Vector3 plate = new Vector3(side * .078f, .048f, -.045f);
            hull.Box(edge, plate, new Vector3(.015f, .067f, .091f), .011f);
            for (int i = 0; i < 3; i++)
            {
                var rotation = Quaternion.AngleAxis(i * 120f, Right);
                var center = plate + new Vector3(side * .011f, 0, 0);
                hull.Bar(rubber, center + rotation * new Vector3(0, .020f, -.013f),
                    center + rotation * new Vector3(0, .020f, .011f), .007f, .006f, .002f);
                hull.Bar(rubber, center + rotation * new Vector3(0, .020f, .011f),
                    center + rotation * new Vector3(0, .009f, .010f), .007f, .006f, .002f);
            }
            Vent(hull, new Vector3(side * .073f, .054f, -.129f), side, 3);
            hull.Cylinder(rubber, new Vector3(side * .083f, .048f, .070f), Right, .040f, .025f);
        }
        Finish(hull, "Kırıcı gövdesi ve saha tutacağı");
        primary = Jaw(true);
        secondary = Jaw(false);
        selector = Part("Kırıcı mafsalı", new Vector3(0, .048f, .071f));
        var rotor = new EcoToolMesh();
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            rotor.Cylinder(metal, Right * (side * .101f), Right, .028f, .020f, 8);
            rotor.Cylinder(accent, Right * (side * .113f), Right, .012f, .009f, 6);
        }
        Finish(rotor, "Mafsal başları", selector);
        var light = new EcoToolMesh();
        light.Box(signal, new Vector3(0, .124f, .072f), new Vector3(.040f, .012f, .024f), .004f);
        Lamp(light, "İşlem ışığı");
        Anchors(new Vector3(0, .166f, -.055f), new Vector3(0, .052f, .236f), new Vector3(-.061f, -.034f, .030f));
    }

    private Transform Jaw(bool upper)
    {
        float sign = upper ? 1f : -1f;
        var root = Part(upper ? "Üst çene" : "Alt çene", new Vector3(0, .048f + sign * .032f, .072f));
        var jaw = new EcoToolMesh();
        jaw.Box(metal, new Vector3(0, sign * .008f, .059f), new Vector3(.125f, .037f, .123f), .015f);
        jaw.Box(shell, new Vector3(0, sign * .008f, .113f), new Vector3(.095f, .033f, .054f), .010f);
        jaw.Box(edge, new Vector3(0, sign * .008f, .136f), new Vector3(.083f, .030f, .029f), .006f);
        for (int i = -1; i <= 1; i++)
            jaw.Box(rubber, new Vector3(i * .029f, -sign * .014f, .101f + (i == 0 ? .005f : 0)),
                new Vector3(.020f, .021f, .076f), .005f);
        Finish(jaw, "Pahlı çene ve dişler", root);
        return root;
    }

    private void Scanner()
    {
        var hull = new EcoToolMesh();
        hull.Box(shell, new Vector3(0, .059f, -.004f), new Vector3(.131f, .113f, .147f), .020f);
        hull.Box(edge, new Vector3(0, .063f, -.059f), new Vector3(.133f, .105f, .053f), .013f);
        Grip(hull, new Vector3(0, .015f, -.019f), new Vector3(0, -.113f, -.059f), .056f);
        hull.Ring(rubber, new Vector3(0, .063f, .063f), Forward, .043f, .071f, .037f, 12);
        hull.Ring(edge, new Vector3(0, .063f, .085f), Forward, .047f, .067f, .020f, 12);
        hull.Box(rubber, new Vector3(0, .119f, -.014f), new Vector3(.101f, .014f, .114f), .008f);
        hull.Box(glass, new Vector3(0, .127f, -.015f), new Vector3(.085f, .006f, .097f), .005f);
        for (int i = 0; i < 6; i++)
        {
            float height = .011f + Mathf.Abs(Mathf.Sin(i * 1.4f)) * .022f;
            hull.Box(edge, new Vector3(-.030f + i * .012f, .131f, -.018f + height * .5f),
                new Vector3(.006f, .003f, height), .001f);
        }
        hull.Ring(edge, new Vector3(0, .132f, -.045f), Up, .009f, .013f, .0024f, 10);
        Finish(hull, "Ekranlı tarayıcı muhafazası");
        primary = Part("Hareketli odak halkası", new Vector3(0, .063f, .087f));
        var optics = new EcoToolMesh();
        optics.Ring(metal, Forward * .003f, Forward, .037f, .059f, .026f, 12);
        for (int i = 0; i < 4; i++)
        {
            var turn = Quaternion.AngleAxis(i * 90f, Forward);
            optics.Box(edge, turn * new Vector3(.048f, 0, .018f), new Vector3(.015f, .009f, .006f), .002f, turn);
        }
        Finish(optics, "Odak tırnakları", primary);
        var light = new EcoToolMesh();
        light.Cylinder(signal, new Vector3(0, 0, .009f), Forward, .039f, .013f, 16);
        Lamp(light, "Tarama merceği", primary);
        selector = Part("Tarama tetiği", new Vector3(.015f, -.015f, .009f));
        var key = new EcoToolMesh();
        key.Box(accent, Vector3.zero, new Vector3(.025f, .021f, .019f), .005f);
        Finish(key, "Tetik düğmesi", selector);
        Anchors(new Vector3(0, -.035f, -.036f), Forward * .022f, barrel: primary);
    }

    private void Flashlight()
    {
        var hull = new EcoToolMesh();
        hull.Lathe(shell, Vector3.zero, Forward, 14, new Vector2(0, -.139f), new Vector2(.036f, -.139f), new Vector2(.042f, -.125f),
            new Vector2(.042f, .041f), new Vector2(.049f, .066f), new Vector2(.061f, .103f),
            new Vector2(.067f, .123f), new Vector2(.065f, .150f), new Vector2(.051f, .153f),
            new Vector2(.043f, .119f), new Vector2(0, .107f));
        hull.Ring(edge, Forward * .145f, Forward, .054f, .071f, .026f, 14);
        hull.Ring(metal, Forward * .154f, Forward, .050f, .067f, .012f, 14);
        hull.Ring(rubber, -Forward * .119f, Forward, .033f, .047f, .032f, 12);
        hull.Ring(rubber, Forward * .054f, Forward, .038f, .051f, .029f, 14);
        hull.Cylinder(rubber, -Forward * .054f, Forward, .043f, .093f, 12);
        for (int i = 0; i < 12; i++)
        {
            var rotation = Quaternion.AngleAxis(i * 30f, Forward);
            for (int j = 0; j < 4; j++)
                hull.Box(edge, rotation * new Vector3(.041f, 0f, -.086f + j * .020f), new Vector3(.008f, .012f, .013f), .003f,
                    rotation * Quaternion.AngleAxis(24f, Right));
        }
        Finish(hull, "Konik reflektör ve tırtıklı sap");
        var lens = new EcoToolMesh();
        lens.Lathe(metal, Vector3.zero, Forward, 14, new Vector2(.016f, .110f),
            new Vector2(.034f, .130f), new Vector2(.052f, .151f));
        lens.Cylinder(signal, Forward * .123f, Forward, .021f, .015f, 12);
        Lamp(lens, "LED ve reflektör");
        selector = Part("Fener düğmesi", new Vector3(0, .046f, .023f));
        var key = new EcoToolMesh();
        key.Box(accent, Vector3.zero, new Vector3(.034f, .012f, .031f), .006f);
        Finish(key, "Açma düğmesi", selector);
        Anchors(new Vector3(0, -.005f, -.049f), Forward * .169f);
    }

    private void Grip(EcoToolMesh mesh, Vector3 shoulder, Vector3 foot, float width)
    {
        mesh.Bar(rubber, shoulder, foot, width, .047f, .012f);
        for (int i = 1; i <= 4; i++)
        {
            var band = Vector3.Lerp(shoulder, foot, i * .18f);
            mesh.Box(edge, band, new Vector3(width + .003f, .009f, .046f), .004f, Quaternion.Euler(17f, 0, 0));
        }
        mesh.Box(shell, foot, new Vector3(width + .011f, .025f, .057f), .008f, Quaternion.Euler(17f, 0, 0));
    }

    private void Vent(EcoToolMesh mesh, Vector3 center, float side, int count)
    {
        for (int i = 0; i < count; i++)
            mesh.Box(rubber, center + new Vector3(side * .009f, (i - (count - 1) * .5f) * .012f, 0),
                new Vector3(.011f, .006f, .030f), .002f, Quaternion.Euler(19, 0, 0));
    }

    private void Leaf(EcoToolMesh mesh, Vector3 at, float side)
    {
        mesh.Lathe(rubber, at, Right, 8, new Vector2(0, -.003f), new Vector2(.024f, -.003f),
            new Vector2(.025f, .003f), new Vector2(0, .003f));
        mesh.Box(edge, at + new Vector3(side * .003f, .001f, 0), new Vector3(.006f, .031f, .015f), .005f,
            Quaternion.AngleAxis(37f, Right));
    }

    private void Bolt(EcoToolMesh mesh, Vector3 at) => mesh.Cylinder(metal, at, Right, .009f, .011f, 6);
}
