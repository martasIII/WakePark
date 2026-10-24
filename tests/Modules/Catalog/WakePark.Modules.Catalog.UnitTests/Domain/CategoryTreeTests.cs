using Shouldly;

using WakePark.BuildingBlocks.Domain;
using WakePark.Modules.Catalog.Domain.Categories;
using WakePark.Modules.Catalog.Domain.Common;

namespace WakePark.Modules.Catalog.UnitTests.Domain;

public sealed class CategoryTreeTests
{
    private static readonly Guid EnterpriseId = Guid.CreateVersion7();

    [Fact]
    public void Kategorie_jde_zaradit_jako_podkategorie_a_zase_vratit_nahoru()
    {
        ProductCategory permanentky = Category("permanentky");
        ProductCategory kratke = Category("kratke");
        List<ProductCategory> all = [permanentky, kratke];

        CategoryTree.Move(kratke, permanentky.Id, all).ShouldBeTrue();
        kratke.ParentId.ShouldBe(permanentky.Id);

        CategoryTree.Move(kratke, null, all).ShouldBeTrue();
        kratke.ParentId.ShouldBeNull();
    }

    [Fact]
    public void Treti_uroven_neprojde()
    {
        ProductCategory root = Category("permanentky");
        ProductCategory child = Category("kratke");
        ProductCategory grandchild = Category("rano");
        List<ProductCategory> all = [root, child, grandchild];
        CategoryTree.Move(child, root.Id, all);

        DomainException error = Should.Throw<DomainException>(() => CategoryTree.Move(grandchild, child.Id, all));

        error.Code.ShouldBe("category.too_deep");
    }

    [Fact]
    public void Kategorii_s_podkategoriemi_nejde_zaradit_pod_jinou()
    {
        ProductCategory jizdne = Category("jizdne");
        ProductCategory permanentky = Category("permanentky");
        ProductCategory kratke = Category("kratke");
        List<ProductCategory> all = [jizdne, permanentky, kratke];
        CategoryTree.Move(kratke, permanentky.Id, all);

        Should.Throw<DomainException>(() => CategoryTree.Move(permanentky, jizdne.Id, all)).Code.ShouldBe("category.too_deep");
    }

    [Fact]
    public void Kategorie_nemuze_byt_pod_sebou_ani_pod_svou_podkategorii()
    {
        ProductCategory root = Category("permanentky");
        ProductCategory child = Category("kratke");
        List<ProductCategory> all = [root, child];
        CategoryTree.Move(child, root.Id, all);

        Should.Throw<DomainException>(() => CategoryTree.Move(root, root.Id, all)).Code.ShouldBe("category.cycle");
        Should.Throw<DomainException>(() => CategoryTree.Move(root, child.Id, all)).Code.ShouldBe("category.cycle");
    }

    [Fact]
    public void Rodic_jineho_provozovatele_neexistuje()
    {
        ProductCategory mine = Category("jizdne");
        ProductCategory foreign = ProductCategory.Create(Guid.CreateVersion7(), "cizi", LocalizedText.Czech("Cizí"), null, 0);

        Should.Throw<DomainException>(() => CategoryTree.Move(mine, foreign.Id, [mine, foreign]))
            .Code.ShouldBe("category.parent_not_found");
    }

    private static ProductCategory Category(string code) =>
        ProductCategory.Create(EnterpriseId, code, LocalizedText.Czech(code), null, 0);
}
