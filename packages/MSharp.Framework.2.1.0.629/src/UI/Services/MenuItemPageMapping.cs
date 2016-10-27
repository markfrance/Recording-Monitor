using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

public class MenuItemPageMapping
{
    SiteMapNode CurrentPage;
    IEnumerable<SiteMapNode> PageHierarchy;

    public MenuItemPageMapping(string itemValue, string pageKey)
    {
        ItemValue = itemValue;
        PageKey = pageKey;

        CurrentPage = System.Web.SiteMap.CurrentNode;
        PageHierarchy = CurrentPage.ParentsHierarchy();
    }

    public void CalculateRelevance()
    {
        if (PageHierarchy.Any(p => p.ResourceKey == PageKey))
        {
            Relevance =  System.Web.SiteMap.Provider.FindByKey(PageKey).GenerationGap(CurrentPage);
        }
        else
        {
            Relevance = null;
        }
    }

    public string ItemValue { get; set; }
    public string PageKey { get; set; }
    public int? Relevance { get; private set; }
}