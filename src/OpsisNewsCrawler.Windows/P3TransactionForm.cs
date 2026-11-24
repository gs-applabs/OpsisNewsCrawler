using OpsisNewsCrawler.Core;
using System.Globalization;

namespace OpsisNewsCrawler.Windows;

/// <summary>Adds an explicit local transaction. This does not overwrite legacy Portfolio positions.</summary>
public sealed class P3TransactionForm : Form
{
    private readonly TickerHost host;
    private readonly FlatText instrument = new() {Width=340};
    private readonly FlatText quantity = new() {Width=170};
    private readonly FlatText price = new() {Width=170};
    private readonly FlatText fees = new() {Width=170};
    private readonly FlatText currency = new() {Width=100};
    private readonly DateTimePicker transactionDate = new() { Format=DateTimePickerFormat.Custom, CustomFormat="dd/MM/yyyy HH:mm", Width=200 };
    private readonly FlatComboBox operation = new() {Width=200};
    private readonly Label status = new(){AutoSize=true,MaximumSize=new Size(680,0)};
    private readonly FlatButton save = new(){Text="Add transaction"};

    public P3TransactionForm(TickerHost host)
    {
        this.host=host;
        Text=Loc.IsGreek?"Συναλλαγή χαρτοφυλακίου":"Portfolio transaction";
        StartPosition=FormStartPosition.CenterParent;
        Size=new Size(700,550);MinimumSize=new Size(550,480);
        Font=new Font("Segoe UI",10f);AutoScaleMode=AutoScaleMode.Dpi;
        var outer=new Panel {Dock=DockStyle.Fill,Padding=new Padding(18)};
        var form=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true,
            FlowDirection=FlowDirection.TopDown,WrapContents=false};
        void Line(string label,Control control)
        {
            form.Controls.Add(new Label {Text=label,AutoSize=true,Margin=new Padding(0,12,0,3)});
            form.Controls.Add(control);
        }
        Line(Loc.IsGreek?"Κλειδί τίτλου (π.χ. Yahoo||AAPL)":"Instrument key (e.g. Yahoo||AAPL)",instrument);
        operation.SetItems(Loc.IsGreek?
            new[]{"Αγορά","Πώληση","Μέρισμα","Split"}:
            new[]{"Buy","Sell","Dividend","Split"},0);
        Line(Loc.IsGreek?"Ενέργεια":"Action",operation);
        transactionDate.MaxDate=DateTime.Now;
        transactionDate.Value=DateTime.Now;
        Line(Loc.IsGreek?"Ημερομηνία και ώρα συναλλαγής":"Transaction date and time",transactionDate);
        Line(Loc.IsGreek?"Ποσότητα (στο split: πολλαπλασιαστής)":"Quantity (split: multiplier)",quantity);
        Line(Loc.IsGreek?"Τιμή ανά μονάδα":"Price per unit",price);
        Line(Loc.IsGreek?"Προμήθειες":"Fees",fees);
        Line(Loc.IsGreek?"Νόμισμα":"Currency",currency);
        currency.Value="EUR";fees.Value="0";
        save.Text=Loc.IsGreek?"Προσθήκη συναλλαγής":"Add transaction";
        save.SetPrimary(true);
        save.Click+=(_,_)=>AddTransaction();
        form.Controls.Add(save);form.Controls.Add(status);
        outer.Controls.Add(form);Controls.Add(outer);
        BackColor=Theme.Background;ForeColor=Theme.Text;status.ForeColor=Theme.TextMuted;
    }

    private void AddTransaction()
    {
        var key=(instrument.Value??"").Trim();
        var cur=(currency.Value??"EUR").Trim().ToUpperInvariant();
        if(key.Length is <1 or >150 || cur.Length!=3 || !cur.All(c=>c is >= 'A' and <= 'Z') ||
           !decimal.TryParse((quantity.Value??"").Replace(',','.'),NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var units) ||
           !decimal.TryParse((price.Value??"").Replace(',','.'),NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var value) ||
           !decimal.TryParse((fees.Value??"0").Replace(',','.'),NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var fee) ||
           units<=0||value<0||fee<0)
        {
            status.Text=Loc.IsGreek?"Έλεγξε κλειδί, νόμισμα και αριθμητικά ποσά.":"Check instrument, currency and numerical fields.";
            return;
        }
        if(host.Config.Portfolio.Any(p=>string.Equals(p.Key,key,StringComparison.OrdinalIgnoreCase) && string.Equals(p.Currency,cur,StringComparison.OrdinalIgnoreCase)))
        {status.Text=Loc.IsGreek?"Η θέση υπάρχει στο παλιό χαρτοφυλάκιο. Μεταφέρετέ την πρώτα από εκεί.":"Holding exists in the legacy portfolio. Move it there first.";return;}
        var action=(PortfolioAction)Math.Clamp(operation.SelectedIndex,0,3);
        if(action==PortfolioAction.Split && (value!=0 || fee!=0))
        {status.Text=Loc.IsGreek?"Split: τιμή και προμήθειες μηδέν.":"Split: zero price and fees.";return;}
        var localTime=DateTime.SpecifyKind(transactionDate.Value,DateTimeKind.Unspecified);
        if(TimeZoneInfo.Local.IsInvalidTime(localTime) || TimeZoneInfo.Local.IsAmbiguousTime(localTime))
        {status.Text=Loc.IsGreek?"Επίλεξε ώρα χωρίς ασάφεια αλλαγής θερινής ώρας.":"Choose an unambiguous local time.";return;}
        var entry=new PortfolioTransaction(key,new DateTimeOffset(localTime,TimeZoneInfo.Local.GetUtcOffset(localTime)),action,units,value,fee,cur);
        try
        {
            var existing=host.Config.PortfolioTransactions.Where(x=>string.Equals(x.InstrumentKey?.Trim(),key,StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Currency?.Trim(),cur,StringComparison.OrdinalIgnoreCase)).ToList();
            PortfolioLedger.Calculate(existing.Append(entry));
            host.Config.PortfolioTransactions.Add(entry);
            host.SaveConfig();
            DialogResult=DialogResult.OK;
            Close();
        }
        catch(Exception ex) when(ex is InvalidOperationException or ArgumentException or OverflowException)
        {
            status.Text=ex.Message;
        }
    }
}
