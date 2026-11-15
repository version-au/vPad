using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using vPad.Core;
using MdBlock = Markdig.Syntax.Block;
using MdInline = Markdig.Syntax.Inlines.Inline;
using WBlock = System.Windows.Documents.Block;
using WInline = System.Windows.Documents.Inline;
using WTable = System.Windows.Documents.Table;

namespace vPad.Ui
{
    /// <summary>
    /// The Markdown preview: Markdig reads the text, and this turns it into a WPF FlowDocument in the theme's colours - headings, lists,
    /// quotes, code, tables, task lists, links (OpenLink) and pictures (beside the file, or on the web). Drawn again a moment after typing
    /// stops, keeping where it was scrolled to.
    /// </summary>
    public sealed class MarkdownView : FlowDocumentScrollViewer
    {
        static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

        public Action<string> OpenLink;
        readonly DispatcherTimer _soon = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        string _text, _folder, _shown;
        string _mono = "Consolas";

        public MarkdownView(AppSettings s)
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            IsToolBarVisible = false;
            Zoom = 100;
            Background = Brushes.Transparent;
            Padding = new Thickness(0);
            _soon.Tick += (o, e) => { _soon.Stop(); Render(); };
            Apply(s);
        }

        public void Apply(AppSettings s)
        {
            _mono = string.IsNullOrWhiteSpace(s.Font) ? "Consolas" : s.Font.Trim() + ", Consolas";
            _shown = null;
            if (_text != null) Render();
        }

        /// <summary>The text changed: drawn again shortly.</summary>
        public void Soon(string text, string folder) { _text = text; _folder = folder; _soon.Stop(); _soon.Start(); }

        /// <summary>Drawn now (it was just shown).</summary>
        public void Now(string text, string folder) { _text = text; _folder = folder; _soon.Stop(); Render(); }

        void Render()
        {
            if (_text == null || _text == _shown) return;
            _shown = _text;
            ScrollViewer sv = FindScroll(this);
            double at = sv != null ? sv.VerticalOffset : 0;
            FlowDocument doc;
            try { doc = Build(Markdown.Parse(_text, Pipeline)); }
            catch (Exception ex)
            {
                Log.Write("Markdown: " + ex.Message);
                doc = Doc();
                doc.Blocks.Add(new Paragraph(new Run("The preview couldn't be drawn: " + ex.Message)));
            }
            Document = doc;
            if (sv != null && at > 0) Dispatcher.BeginInvoke(new Action(() => { ScrollViewer s2 = FindScroll(this); if (s2 != null) s2.ScrollToVerticalOffset(at); }), DispatcherPriority.Loaded);
        }

        static ScrollViewer FindScroll(DependencyObject o)
        {
            if (o == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++)
            {
                DependencyObject c = VisualTreeHelper.GetChild(o, i);
                var sv = c as ScrollViewer;
                if (sv != null) return sv;
                sv = FindScroll(c);
                if (sv != null) return sv;
            }
            return null;
        }

        FlowDocument Doc()
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"), FontSize = 14.5, PagePadding = new Thickness(26, 18, 26, 30),
                TextAlignment = TextAlignment.Left, LineHeight = 22, IsOptimalParagraphEnabled = false, Background = Brushes.Transparent
            };
            doc.SetResourceReference(FlowDocument.ForegroundProperty, "vx.Fg");
            return doc;
        }

        FlowDocument Build(MarkdownDocument md)
        {
            FlowDocument doc = Doc();
            foreach (MdBlock b in md) { WBlock w = BlockOf(b); if (w != null) doc.Blocks.Add(w); }
            return doc;
        }

        // =================================================================
        // Blocks
        // =================================================================
        WBlock BlockOf(MdBlock b)
        {
            var heading = b as HeadingBlock;
            if (heading != null)
            {
                double[] sizes = { 0, 26, 21, 18, 16, 15, 14 };
                var p = new Paragraph { FontSize = sizes[Math.Min(6, Math.Max(1, heading.Level))], FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, heading.Level <= 2 ? 14 : 10, 0, 8), LineHeight = double.NaN };
                AddInlines(p.Inlines, heading.Inline);
                if (heading.Level <= 2)
                {
                    p.BorderThickness = new Thickness(0, 0, 0, 1);
                    p.Padding = new Thickness(0, 0, 0, 6);
                    p.SetResourceReference(Paragraph.BorderBrushProperty, "vx.Line");
                }
                return p;
            }
            var para = b as ParagraphBlock;
            if (para != null)
            {
                var p = new Paragraph { Margin = new Thickness(0, 0, 0, 10) };
                AddInlines(p.Inlines, para.Inline);
                return p;
            }
            var list = b as ListBlock;
            if (list != null)
            {
                var l = new List { MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc, Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(22, 0, 0, 0) };
                int start;
                if (list.IsOrdered && int.TryParse(list.OrderedStart, out start)) l.StartIndex = Math.Max(1, start);
                foreach (MdBlock item in list)
                {
                    var li = new ListItem();
                    var container = item as ContainerBlock;
                    if (container != null)
                        foreach (MdBlock inner in container)
                        {
                            WBlock w = BlockOf(inner);
                            if (w == null) continue;
                            if (w is Paragraph) w.Margin = new Thickness(0, 0, 0, 3);
                            li.Blocks.Add(w);
                        }
                    if (TaskDone(item) != null) l.MarkerStyle = TextMarkerStyle.None;
                    l.ListItems.Add(li);
                }
                return l;
            }
            var quote = b as QuoteBlock;
            if (quote != null)
            {
                var sec = new Section { Padding = new Thickness(14, 2, 0, 2), Margin = new Thickness(0, 0, 0, 10), BorderThickness = new Thickness(3, 0, 0, 0) };
                sec.SetResourceReference(Section.BorderBrushProperty, "vx.Accent");
                sec.SetResourceReference(Section.ForegroundProperty, "vx.FgDim");
                foreach (MdBlock inner in quote) { WBlock w = BlockOf(inner); if (w != null) sec.Blocks.Add(w); }
                return sec;
            }
            var code = b as CodeBlock;   // fenced and indented
            if (code != null)
            {
                string text = string.Join("\n", code.Lines.Lines.Take(code.Lines.Count).Select(x => x.Slice.ToString()));
                var p = new Paragraph(new Run(text))
                {
                    FontFamily = new FontFamily(_mono), FontSize = 13, LineHeight = double.NaN, Padding = new Thickness(12, 9, 12, 9),
                    Margin = new Thickness(0, 0, 0, 12), BorderThickness = new Thickness(1)
                };
                p.SetResourceReference(Paragraph.BackgroundProperty, "vx.Panel");
                p.SetResourceReference(Paragraph.BorderBrushProperty, "vx.Line");
                return p;
            }
            if (b is ThematicBreakBlock)
            {
                var p = new Paragraph { Margin = new Thickness(0, 6, 0, 14), BorderThickness = new Thickness(0, 1, 0, 0), FontSize = 1, LineHeight = 1 };
                p.SetResourceReference(Paragraph.BorderBrushProperty, "vx.Line");
                return p;
            }
            var html = b as HtmlBlock;
            if (html != null)
            {
                string text = string.Join("\n", html.Lines.Lines.Take(html.Lines.Count).Select(x => x.Slice.ToString()));
                if (text.TrimStart().StartsWith("<!--")) return null;
                var p = new Paragraph(new Run(text)) { FontFamily = new FontFamily(_mono), FontSize = 12.5, Margin = new Thickness(0, 0, 0, 10) };
                p.SetResourceReference(Paragraph.ForegroundProperty, "vx.FgDim");
                return p;
            }
            var table = b as Markdig.Extensions.Tables.Table;
            if (table != null) return TableOf(table);
            var container2 = b as ContainerBlock;
            if (container2 != null)
            {
                var sec = new Section();
                foreach (MdBlock inner in container2) { WBlock w = BlockOf(inner); if (w != null) sec.Blocks.Add(w); }
                return sec;
            }
            var leaf = b as LeafBlock;
            if (leaf != null && leaf.Inline != null)
            {
                var p = new Paragraph { Margin = new Thickness(0, 0, 0, 10) };
                AddInlines(p.Inlines, leaf.Inline);
                return p;
            }
            return null;
        }

        static bool? TaskDone(MdBlock item)
        {
            var c = item as ContainerBlock;
            var p = c != null ? c.FirstOrDefault() as ParagraphBlock : null;
            var t = p != null && p.Inline != null ? p.Inline.FirstChild as TaskList : null;
            return t != null ? t.Checked : (bool?)null;
        }

        WTable TableOf(Markdig.Extensions.Tables.Table t)
        {
            var wt = new WTable { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 12), BorderThickness = new Thickness(1, 1, 0, 0) };
            wt.SetResourceReference(WTable.BorderBrushProperty, "vx.Line");
            int cols = t.OfType<Markdig.Extensions.Tables.TableRow>().Select(r => r.Count).DefaultIfEmpty(1).Max();
            for (int i = 0; i < cols; i++) wt.Columns.Add(new TableColumn());
            var group = new TableRowGroup();
            foreach (Markdig.Extensions.Tables.TableRow r in t.OfType<Markdig.Extensions.Tables.TableRow>())
            {
                var row = new System.Windows.Documents.TableRow();
                if (r.IsHeader) { row.FontWeight = FontWeights.SemiBold; row.SetResourceReference(System.Windows.Documents.TableRow.BackgroundProperty, "vx.Panel"); }
                for (int i = 0; i < r.Count; i++)
                {
                    var cell = new System.Windows.Documents.TableCell { Padding = new Thickness(8, 4, 8, 4), BorderThickness = new Thickness(0, 0, 1, 1) };
                    cell.SetResourceReference(System.Windows.Documents.TableCell.BorderBrushProperty, "vx.Line");
                    var mdCell = r[i] as Markdig.Extensions.Tables.TableCell;
                    if (mdCell != null)
                        foreach (MdBlock inner in mdCell)
                        {
                            WBlock w = BlockOf(inner);
                            if (w == null) continue;
                            w.Margin = new Thickness(0);
                            if (i < t.ColumnDefinitions.Count && w is Paragraph)
                            {
                                TableColumnAlign? a = t.ColumnDefinitions[i].Alignment;
                                if (a == TableColumnAlign.Center) ((Paragraph)w).TextAlignment = TextAlignment.Center;
                                else if (a == TableColumnAlign.Right) ((Paragraph)w).TextAlignment = TextAlignment.Right;
                            }
                            cell.Blocks.Add(w);
                        }
                    row.Cells.Add(cell);
                }
                group.Rows.Add(row);
            }
            wt.RowGroups.Add(group);
            return wt;
        }

        // =================================================================
        // Inlines
        // =================================================================
        void AddInlines(InlineCollection into, ContainerInline from)
        {
            if (from == null) return;
            foreach (MdInline i in from) { WInline w = InlineOf(i); if (w != null) into.Add(w); }
        }

        WInline InlineOf(MdInline i)
        {
            var lit = i as LiteralInline;
            if (lit != null) return new Run(lit.Content.ToString());
            var task = i as TaskList;
            if (task != null)
            {
                var r = new Run(task.Checked ? "  " : "  ") { FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 12 };
                if (task.Checked) r.SetResourceReference(TextElement.ForegroundProperty, "vx.Accent");
                return r;
            }
            var em = i as EmphasisInline;
            if (em != null)
            {
                Span s;
                if (em.DelimiterChar == '~') { s = new Span(); s.TextDecorations = TextDecorations.Strikethrough; }
                else if (em.DelimiterCount >= 2) s = new Bold();
                else s = new Italic();
                AddInlines(s.Inlines, em);
                return s;
            }
            var code = i as CodeInline;
            if (code != null)
            {
                var r = new Run(" " + code.Content + " ") { FontFamily = new FontFamily(_mono), FontSize = 13 };
                r.SetResourceReference(TextElement.BackgroundProperty, "vx.Panel");
                r.SetResourceReference(TextElement.ForegroundProperty, "vx.Secondary");
                return r;
            }
            var auto = i as AutolinkInline;
            if (auto != null) return Link(auto.Url, new Run(auto.Url));
            var link = i as LinkInline;
            if (link != null)
            {
                if (link.IsImage) return Picture(link.Url, string.Concat(link.Descendants<LiteralInline>().Select(x => x.Content.ToString())));
                var span = new Span();
                AddInlines(span.Inlines, link);
                if (span.Inlines.Count == 0) span.Inlines.Add(new Run(link.Url));
                return Link(link.Url, span);
            }
            var br = i as LineBreakInline;
            if (br != null) return br.IsHard ? (WInline)new LineBreak() : new Run(" ");
            var html = i as HtmlInline;
            if (html != null)
            {
                string tag = html.Tag ?? "";
                if (tag.StartsWith("<br", StringComparison.OrdinalIgnoreCase)) return new LineBreak();
                return null;   // other inline HTML tags are left out; their text shows
            }
            var ent = i as HtmlEntityInline;
            if (ent != null) return new Run(ent.Transcoded.ToString());
            var container = i as ContainerInline;
            if (container != null) { var s = new Span(); AddInlines(s.Inlines, container); return s; }
            return null;
        }

        WInline Link(string url, WInline content)
        {
            var h = new Hyperlink(content) { Cursor = System.Windows.Input.Cursors.Hand, TextDecorations = null, ToolTip = url };
            h.SetResourceReference(TextElement.ForegroundProperty, "vx.Accent");
            h.MouseEnter += (o, e) => h.TextDecorations = TextDecorations.Underline;
            h.MouseLeave += (o, e) => h.TextDecorations = null;
            string u = url;
            h.Click += (o, e) => { if (OpenLink != null) OpenLink(u); };
            return h;
        }

        WInline Picture(string url, string alt)
        {
            try
            {
                Uri uri;
                if (!Uri.TryCreate(url, UriKind.Absolute, out uri) || uri.Scheme == "file")
                {
                    if (_folder == null) return new Run("[" + alt + "]");
                    string p = Path.GetFullPath(Path.Combine(_folder, Uri.UnescapeDataString(url.Split('?', '#')[0])));
                    if (!File.Exists(p)) return new Run("[" + alt + "]");
                    uri = new Uri(p);
                }
                else if (uri.Scheme != "http" && uri.Scheme != "https") return new Run("[" + alt + "]");
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = uri;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bmp.EndInit();
                var img = new Image { Source = bmp, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxHeight = 600, ToolTip = alt };
                return new InlineUIContainer(img) { BaselineAlignment = BaselineAlignment.Bottom };
            }
            catch (Exception ex) { Log.Write("Markdown picture " + url + ": " + ex.Message); return new Run("[" + alt + "]"); }
        }
    }
}
