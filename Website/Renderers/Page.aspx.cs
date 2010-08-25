﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Web;
using System.Web.UI;
using Composite;
using Composite.Data;
using Composite.Data.Types;
using Composite.Pages;
using Composite.Renderings;
using Composite.Renderings.Page;
using Composite.Security;
using Composite.WebClient;


public partial class Renderers_Page : System.Web.UI.Page
{
    private IDisposable _dataScope;

    private PageUrl _url;
    private NameValueCollection _foreignQueryStringParameters;
    private string _cacheUrl = null;


    public Renderers_Page()
    {
        string query = HttpContext.Current.Request.Url.OriginalString;

        _url = PageUrl.Parse(query, out _foreignQueryStringParameters);

        if (_url.PublicationScope != PublicationScope.Public)
        {
            if (UserValidationFacade.IsLoggedIn() == false)
            {
                HttpContext.Current.Response.Redirect(string.Format("/Composite/Login.aspx?ReturnUrl={0}", HttpUtility.UrlEncodeUnicode(HttpContext.Current.Request.Url.OriginalString)), false);
                HttpContext.Current.ApplicationInstance.CompleteRequest();
                return;
            }
        }

       _cacheUrl = HttpContext.Current.Request.Url.PathAndQuery;

       RewritePath();
    }



    protected void Page_Init(object sender, EventArgs e)
    {
        if (_url.PublicationScope != PublicationScope.Public)
        {
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
        }

        _dataScope = new DataScope(DataScopeIdentifier.FromPublicationScope(_url.PublicationScope), _url.Locale); // IDisposable, Disposed in OnUnload

        var pageManager = Composite.Pages.PageManager.Create(_url.PublicationScope, _url.Locale);

        IPage page = pageManager.GetPageById(_url.PageId);

        if (page != null)
        {
            RenderingResponseHandlerResult responseHandling = RenderingResponseHandlerFacade.GetDataResponseHandling(page.GetDataEntityToken());

            if ((responseHandling != null) && (responseHandling.PreventPublicCaching == true))
            {
                Response.Cache.SetCacheability(HttpCacheability.NoCache);
            }

            if ((responseHandling != null) &&
                (responseHandling.EndRequest || responseHandling.RedirectRequesterTo != null))
            {
                if (responseHandling.RedirectRequesterTo != null)
                {
                    Response.Redirect(responseHandling.RedirectRequesterTo.AbsoluteUri, false);
                }

                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            IEnumerable<IPagePlaceholderContent> contents = pageManager.GetPlaceholdersContent(page.Id);
            Control renderedPage = PageRenderer.Render(page, contents);
            this.Controls.Add(renderedPage);
            if (this.Form != null) this.Form.Action = Request.RawUrl;
        }
        else
        {
            // GUID not found in lookup
            this.Controls.Add(new LiteralControl("<div>Unknown page id - either this page has not been published yet or it has been deleted.</div>"));
        }
    }


    private void RewritePath()
    {
        UrlBuilder structuredUrl =  _url.Build(PageUrlType.Public);

        if (structuredUrl == null)
        {
            return;
        }

        structuredUrl.AddQueryParameters(_foreignQueryStringParameters);

        HttpContext.Current.RewritePath(structuredUrl.FilePath, structuredUrl.PathInfo, structuredUrl.QueryString);
    }


    protected override void Render(HtmlTextWriter writer)
    {
        ScriptManager scriptManager = ScriptManager.GetCurrent(this);
        bool isUpdatePanelPostback = scriptManager != null && scriptManager.IsInAsyncPostBack;

        if (isUpdatePanelPostback == true)
        {
            base.Render(writer);
            return;
        }

        StringBuilder markupBuilder = new StringBuilder();
        StringWriter sw = new StringWriter(markupBuilder);
        base.Render(new HtmlTextWriter(sw));

        string xhtml = PageUrlHelper.ChangeRenderingPageUrlsToPublic(markupBuilder.ToString());
        xhtml = Composite.Xml.XhtmlPrettifier.Prettify(xhtml);

        writer.Write(xhtml);
    }

    protected override void OnUnload(EventArgs e)
    {
        base.OnUnload(e);

        if (_dataScope != null)
            _dataScope.Dispose();

        // Rewrite path to what it was when this page was constructed. This ensure full page caching can work.
        HttpContext.Current.RewritePath(_cacheUrl);
    }

}
