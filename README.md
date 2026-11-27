<p align="center"><img src="https://raw.githubusercontent.com/gs-applabs/gs-applabs/main/assets/projects/crawler.svg" width="100%" alt="OPSIS News Crawler — News, markets and weather"/></p>

<p align="center"><a href="https://github.com/gs-applabs/OpsisNewsCrawler/releases/latest"><img alt="Latest published release" src="https://img.shields.io/github/v/release/gs-applabs/OpsisNewsCrawler?label=release"/></a> <a href="https://github.com/gs-applabs/OpsisNewsCrawler/releases"><img alt="All release asset downloads, not unique users" src="https://img.shields.io/github/downloads/gs-applabs/OpsisNewsCrawler/total?label=asset%20downloads"/></a> <a href="https://github.com/gs-applabs/OpsisNewsCrawler/actions/workflows/windows-ci.yml"><img alt="CI workflow status" src="https://github.com/gs-applabs/OpsisNewsCrawler/actions/workflows/windows-ci.yml/badge.svg"/></a> <a href="https://github.com/gs-applabs/OpsisNewsCrawler/actions/workflows/product-snapshots.yml"><img alt="Screenshot workflow status" src="https://github.com/gs-applabs/OpsisNewsCrawler/actions/workflows/product-snapshots.yml/badge.svg"/></a></p>

<p align="center"><a href="docs/GS-APP-LABS-FAQ.md">FAQ / Συχνές ερωτήσεις</a> · <a href="https://github.com/gs-applabs/OpsisNewsCrawler/issues/new/choose">Feedback / Προτάσεις</a></p>

<p align="center"><strong>Follow news, markets and weather without leaving your Windows desktop.</strong><br/>Η ενημέρωση στο επίκεντρο της επιφάνειας εργασίας σου.</p>

<p align="center"><a href="https://github.com/gs-applabs/OpsisNewsCrawler/releases/download/v0.30.0/OpsisNewsCrawler-Setup.exe"><img alt="Download v0.30.0" src="https://img.shields.io/badge/DOWNLOAD-v0.30.0-087EA4?style=for-the-badge"/></a> <a href="https://github.com/gs-applabs/OpsisNewsCrawler/releases/latest"><img alt="Release notes" src="https://img.shields.io/badge/RELEASE-NOTES-334155?style=for-the-badge"/></a></p>

<p align="center"><a href="#at-a-glance--με-μια-ματιά">Overview / Επισκόπηση</a> · <a href="#full-documentation--αναλυτική-τεκμηρίωση">Full documentation / Τεκμηρίωση</a> · <a href="https://github.com/gs-applabs/OpsisNewsCrawler/issues">Support / Υποστήριξη</a> · <a href="https://github.com/gs-applabs">GS App Labs</a></p>

## Current screenshot gallery / Νέα στιγμιότυπα

<a href="docs/product-snapshots/snapshot-bar/snapshot-bar.png"><img src="docs/product-snapshots/snapshot-bar/snapshot-bar.png" width="100%" alt="Desktop news and market tickers"/></a>

<table>
<tr>
<td width="50%" valign="top"><a href="docs/product-snapshots/snapshot-portfolio/snapshot-portfolio.png"><img src="docs/product-snapshots/snapshot-portfolio/snapshot-portfolio.png" width="100%" alt="Portfolio / Χαρτοφυλάκιο"/></a><br/><sub>Portfolio / Χαρτοφυλάκιο</sub></td>
<td width="50%" valign="top"><a href="docs/product-snapshots/hazards/snapshot-hazard-icons.png"><img src="docs/product-snapshots/hazards/snapshot-hazard-icons.png" width="100%" alt="Hazard indicators / Δείκτες κινδύνων"/></a><br/><sub>Hazard indicators / Δείκτες κινδύνων</sub></td>
</tr>
</table>

**[Browse application screenshots / Προβολή στιγμιοτύπων](docs/product-snapshots/README.md)** · [Capture workflow / Μηχανισμός λήψης](../../actions/workflows/product-snapshots.yml)

<sub>Application-rendered previews, including synthetic analytics and hazard illustrations. / Πραγματική απεικόνιση με ενδεικτικά δεδομένα αναλύσεων και κινδύνων.</sub>

## At a glance / Με μια ματιά

| | What you can do / Τι μπορείς να κάνεις |
|---|---|
| **Live feeds / Ροές** | Desktop ticker, RSS and weather / Μπάρα ειδήσεων, RSS και καιρός |
| **Intelligence / Πληροφόρηση** | Topic timelines, article comparison and search / Χρονολόγια, συγκρίσεις και αναζήτηση |
| **Analytics / Αναλύσεις** | Market and news visualizations, optional local archive / Οπτικοποιήσεις και προαιρετικό αρχείο |

### Get started / Ξεκίνημα

- **Installer:** [OpsisNewsCrawler-Setup.exe](https://github.com/gs-applabs/OpsisNewsCrawler/releases/download/v0.30.0/OpsisNewsCrawler-Setup.exe) · [SHA-256](https://github.com/gs-applabs/OpsisNewsCrawler/releases/download/v0.30.0/OpsisNewsCrawler-Setup.exe.sha256)
- **Requirements / Απαιτήσεις:** Windows 11 · .NET 11 Desktop Runtime · optional OPSIS host.
- **What's new / Νέα έκδοση:** News intelligence, analytics and independent Chronikologio integration. / Νέο κέντρο πληροφόρησης και αναλύσεων.
- **Known limits / Όρια:** Text heuristics are not fact-checking; market analytics are not investment advice. Interactive Windows acceptance testing is not complete. / Οι αναλύσεις δεν αποτελούν επαλήθευση ειδήσεων ή επενδυτική σύσταση.

See the [published release](https://github.com/gs-applabs/OpsisNewsCrawler/releases/tag/v0.30.0) for verification details. Review installation prompts and compare the checksum before running the installer. / Δείτε τις σημειώσεις έκδοσης και επαληθεύστε το SHA-256 πριν από την εγκατάσταση.

---

## Full documentation / Αναλυτική τεκμηρίωση

<p align="center"><img src="docs/img/icon-wide.png" width="560" alt="Opsis News Crawler"/></p>

<h1 align="center">Opsis News Crawler</h1>

<p align="center">
<b>Ειδήσεις, αγορές και καιρός σε μια λεπτή, διάφανη μπάρα μέσα στη γραμμή εργασιών των Windows.</b><br/>
<i>News, markets and weather in a slim, transparent bar inside your Windows taskbar.</i>
</p>

<p align="center">
<a href="../../releases/latest"><img alt="release" src="https://img.shields.io/github/v/release/gs-applabs/OpsisNewsCrawler"/></a>
<a href="LICENSE"><img alt="license MIT" src="https://img.shields.io/badge/license-MIT-blue"/></a>
<img alt="Windows 11" src="https://img.shields.io/badge/Windows-11-0078d4"/>
<img alt=".NET 11" src="https://img.shields.io/badge/.NET-11-512bd4"/>
<img alt="14 languages" src="https://img.shields.io/badge/languages-14-green"/>
</p>

- [Ελληνικά](#ελληνικά) · [English](#english) · [Η οικογένεια Opsis / The Opsis family](#η-οικογένεια-opsis--the-opsis-family) · [Άδεια / License](#άδεια--license)

---

## Ελληνικά

Μια μπάρα ειδήσεων, τιμών αγορών και καιρού που κυλά στην επιφάνεια εργασίας των Windows — πάνω από τη γραμμή εργασιών,
**μέσα** σε αυτήν, ή στο κέντρο της οθόνης. Τρέχει μόνη της (εικονίδιο στην περιοχή ειδοποιήσεων) ή ως plugin του
**Opsis - Desktop Customization**. Δωρεάν και ανοιχτού κώδικα (MIT).

<p align="center"><img src="docs/screenshots/bar-news-1.png" width="900" alt="η μπάρα"/><br/><img src="docs/screenshots/bar-news-2.png" width="900" alt="η μπάρα"/><br/><img src="docs/screenshots/bar-hazards-weather.png" width="900" alt="φυσικές καταστροφές και καιρός"/></p>

### Λειτουργίες

| Τι | Περιγραφή |
|---|---|
| 📰 Ειδήσεις | RSS από πολλές πηγές σε **6 κατηγορίες** (Οικονομία, Αγορές, Διεθνή, Ελλάδα, Άμυνα & Διπλωματία, Αθλητικά) και δικές σας πηγές· η ίδια είδηση από πολλές πηγές εμφανίζεται μία φορά· έκτακτες με κόκκινη μπάρα, ήχο και σύνδεσμο σε ζωντανό βίντεο· «για αργότερα» με Ctrl+κλικ |
| 📈 Τιμές | Μετοχές Euronext (Αθήνα με 15′ καθυστέρηση, Παρίσι, Άμστερνταμ, Βρυξέλλες, Λισσαβώνα, Μιλάνο, Όσλο), μέταλλα, πετρέλαιο/αέριο, ισοτιμίες ΕΚΤ· ειδοποιήσεις ορίων· οι τιμές επισημαίνονται ως καθυστερημένες |
| 💼 Χαρτοφυλάκιο και ανάλυση | Αξία και κέρδος/ζημιά, RSI, SMA20/50, MACD, κεριά, δοκιμή στρατηγικής, δείκτης φόβου και απληστίας, διάθεση ειδήσεων |
| 🗂 Θέματα και ιστορίες | Λίστες παρακολούθησης θεμάτων (λέξεις-κλειδιά και περιοχή), ομαδοποίηση δημοσιευμάτων ανά γεγονός με σύγκριση πηγών (δεν είναι έλεγχος αλήθειας), τοπικό χρονολόγιο μαζί με τις εφαρμογές Opsis |
| 🔎 Αναζήτηση και εστίαση | Αναζήτηση στις ειδήσεις, λειτουργία εστίασης με πληκτρολόγιο, δύο δημοσιεύματα δίπλα δίπλα, αναδυόμενοι όροι, χρονολόγιο ιστορίας και ενδείξεις διαφορετικών αριθμών, κοινής πηγής και χώρας |
| 📊 Αναλύσεις | Πρόσωπα και σχέσεις, κάλυψη, τάσεις, παγκόσμιος χάρτης, χάρτης αγορών, μεταβλητότητα και υποχώρηση, κατανομή και χρονικά σταθμισμένη απόδοση, ημερολόγιο μερισμάτων και δεικτών, προαιρετικό τοπικό αρχείο ειδήσεων (όχι επενδυτική σύσταση) |
| 📅 Χρονικολόγιο χωρίς Opsis | Η ροή «Σαν σήμερα» φτάνει στις μπάρες και όταν τα δύο προγράμματα τρέχουν χωριστά |
| 📤 Εξαγωγές | Ημερήσια και εβδομαδιαία σύνοψη HTML (χωρίς δίκτυο, ασφαλής κωδικοποίηση, εκτύπωση σε PDF από τον browser), οικονομικό ημερολόγιο με φίλτρα (εισαγωγή JSON, εξαγωγή ICS), playlist M3U για το Opsis Media Suite |
| 🧾 Ledger χαρτοφυλακίου | Αγορές, πωλήσεις, μερίσματα, split και προμήθειες ανά νόμισμα (χωρίς αυτόματη μετατροπή), σταθμικό κόστος, πραγματοποιημένο κέρδος/ζημιά, αποτίμηση από την τελευταία γνωστή τιμή· οι παλιές θέσεις μεταφέρονται ως αρχικό υπόλοιπο με επιβεβαίωση και επιστρέφουν αν αποτύχει η αποθήκευση |
| 🔔 Ειδοποιήσεις τιμής | Τιμή ή ημερήσια μεταβολή %, περιθώριο επανεξοπλισμού, μέγιστη ηλικία δεδομένων· δεν ενεργοποιούνται από μηδενικές ή ελλιπείς τιμές |
| 📺 Live Video Hub | Προαιρετικό: κανάλια της επιλογής σας, σίγαση, χωρίς αυτόματη αναπαραγωγή |
| 🧱 Widgets Opsis | Πέντε προαιρετικά widgets (ειδήσεις, αγορές, καιρός, έκτακτα, ιστορικά γεγονότα), studio και gallery ειδήσεων, υψηλή αντίθεση, πλοήγηση με πληκτρολόγιο |
| 🛡 Σταθερότητα και ασφάλεια | Όρια ταυτόχρονων λήψεων, backoff και Retry-After, όριο 5 MB ανά feed, φρουρός ανακατευθύνσεων (όχι προς τοπικές διευθύνσεις ή από https σε http), safe mode, λιγότερες εγγραφές στον δίσκο, εγκατάσταση με αντίγραφο επαναφοράς |
| ⛅ Καιρός | Πόλεις της Ελλάδας: εικονίδιο, θερμοκρασία, άνεμος, υγρασία· χάρτης με κλικ· προειδοποιήσεις έντονων φαινομένων |
| 🌋 Φυσικές καταστροφές | Σεισμοί, πυρκαγιές, πλημμύρες, καταιγίδες, ηφαίστεια ως περιεχόμενο μπάρας· φίλτρα περιοχής, μεγέθους, ηλικίας και ειδών· συγχώνευση αναφορών πολλών οργανισμών |
| 🖥 Μπάρες | Πάνω από τη γραμμή εργασιών ή **μέσα** σε αυτήν (ακολουθεί και την αυτόματη απόκρυψη)· δεύτερη μπάρα, μπάρα ανά οθόνη, μπάρα επιφάνειας που αντικαθιστά την κρυμμένη μπάρα εργασιών· κάθε μπάρα δείχνει ειδήσεις, αγορές ή καιρό |
| 🎨 Εμφάνιση | Θέματα (ίδια με το Opsis), χρώμα, διαφάνεια, υλικό φόντου (acrylic/blur), σβήσιμο στα άκρα· πλήρης πλοήγηση με πληκτρολόγιο |
| 🧩 Plugin ή αυτόνομη | Ενσωματωμένη στο Opsis (δική της σελίδα, γλώσσα και χρώματα του Opsis) ή ξεχωριστή εφαρμογή με εικονίδιο στην περιοχή ειδοποιήσεων |
| 🌍 14 γλώσσες | Ελληνικά, English, Deutsch, Français, Español, Italiano, Português, Русский, Türkçe, 日本語, 한국어, 简体中文, العربية (δεξιά-προς-αριστερά), हिन्दी |

**Νέο στην 0.16.1:** σελίδα «Ροή καταστροφών», ξεχωριστή ταχύτητα ανά μπάρα και ανά μισό split, κατάλογος Yahoo με 100+ προϊόντα προς επιλογή, ενιαία πλάτη καρτών σε όλες τις γλώσσες.

**Νέο στην 0.16.0:** **φυσικές καταστροφές** (σεισμοί, πυρκαγιές, πλημμύρες, καταιγίδες, ηφαίστεια από USGS, EMSC, GDACS, NASA EONET· ο ίδιος σεισμός από πολλούς οργανισμούς δείχνει μέσο όρο μεγέθους και εύρος), **καιρός για οποιονδήποτε τόπο** (όνομα, ταχυδρομικός κώδικας ή συντεταγμένες), το κινούμενο εικονίδιο δείχνει τον τρέχοντα καιρό και την υψηλότερη ενεργή ειδοποίηση, ροδέλα ποντικιού, πετρέλαιο/αέριο από συμβόλαια Yahoo (προαιρετικά).

**Νέο στην 0.15.0:** οδηγός καλωσορίσματος και παράθυρο «Η οικογένεια Opsis» με κινούμενο εικονίδιο (εφημερίδα, μπάλα, ασπίδα, οικονομία, καιρός, φυσικές καταστροφές), ειδοποίηση νέας έκδοσης μία φορά ανά έκδοση, ορθογώνιο εικονίδιο, θέματα Opsis, σωστή ανίχνευση της μπάρας εργασιών με αυτόματη απόκρυψη, πλήρης υποστήριξη πληκτρολογίου στα στοιχεία ελέγχου. Όλο το ιστορικό: [`CHANGELOG.md`](CHANGELOG.md).

**Νέες επιλογές P1:** Το widget Ειδήσεων μπορεί να προβάλλει επιλεγμένη κατηγορία από τις ήδη διαμορφωμένες πηγές RSS (π.χ. Ελλάδα ή Διεθνή), χωρίς αυτόματο εντοπισμό τοποθεσίας. Το widget Αγορών υποστηρίζει προαιρετικές οικονομικές κάρτες που εμφανίζουν το τελευταίο διαθέσιμο στοιχείο, όχι κατ' ανάγκη ζωντανή τιμή. Δεν προστίθεται νέος πάροχος ή πρόσθετο πρόγραμμα παρασκηνίου.

**P1 (υπό ανάπτυξη, όχι ακόμη δημοσιευμένο):** Στις Ρυθμίσεις → Στούντιο widgets επιλέγεις για τα πέντε υπάρχοντα widgets τύπο προβολής, έως 30 εγγραφές, μέγεθος κειμένου 80–160%, στοιχεία πηγής/ώρας και προαιρετικό χρώμα έμφασης Opsis. Για Ειδήσεις, Έκτακτες και Σαν Σήμερα διατίθεται η προβολή καρτών **Newsroom Gallery**. Υπάρχουν πέντε προφίλ με ένα κλικ· το **Easy Read** χρησιμοποιεί μεγαλύτερα γράμματα. Οι αποθηκευμένες θέσεις στην επιφάνεια εργασίας εξακολουθούν να ανήκουν στο Opsis.

### Στιγμιότυπα οθόνης

Σκούρο και φωτεινό θέμα (όλα: [`docs/screenshots`](docs/screenshots)).

<table>
<tr>
<td><img src="docs/screenshots/el/dark/02-bars.png" width="440"/><br/><sub>Μπάρες (σκούρο)</sub></td>
<td><img src="docs/screenshots/el/light/05-news.png" width="440"/><br/><sub>Ροή ειδήσεων (φωτεινό)</sub></td>
</tr>
<tr>
<td><img src="docs/screenshots/el/dark/06-markets.png" width="440"/><br/><sub>Αγορές</sub></td>
<td><img src="docs/screenshots/el/light/07-weather.png" width="440"/><br/><sub>Καιρός</sub></td>
</tr>
<tr>
<td><img src="docs/screenshots/welcome/welcome-el-1.png" width="440"/><br/><sub>Οδηγός καλωσορίσματος</sub></td>
<td><img src="docs/screenshots/welcome/family-el.png" width="440"/><br/><sub>Η οικογένεια Opsis</sub></td>
</tr>
<tr>
<td><img src="docs/screenshots/el/dark/09-analysis-candles.png" width="440"/><br/><sub>Ανάλυση αγορών</sub></td>
<td><img src="docs/screenshots/el/dark/03-look.png" width="440"/><br/><sub>Εμφάνιση</sub></td>
</tr>
<tr>
<td><img src="docs/screenshots/el/dark/10-weather-search.png" width="440"/><br/><sub>Καιρός: εύρεση οποιουδήποτε τόπου</sub></td>
<td><img src="docs/screenshots/el/dark/11-disasters.png" width="440"/><br/><sub>Ροή καταστροφών</sub></td>
</tr>
</table>

### Εγκατάσταση

**Απαιτήσεις:** Windows 11 (64-bit· τα Windows 10 δεν έχουν δοκιμαστεί), [.NET 11 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/11.0) (ο installer το προτείνει και το κατεβάζει μόνο αν το επιτρέψετε), προαιρετικά WebView2 Runtime (ζωντανό βίντεο έκτακτων).

**Συμβατότητα 0.28.0:** ως plugin απαιτεί **Opsis 2.8 ή 3.0** (κοινό συμβόλαιο plugin 1.0.0.0, ακριβώς το ίδιο DLL με το host, και Opsis.UI 0.6.0)· ο installer αρνείται την ενσωμάτωση αν το DLL του host διαφέρει και τότε εγκαθίσταται αυτόνομα. Όλες οι εφαρμογές της οικογένειας τρέχουν σε .NET 11. Οι ρυθμίσεις σας διατηρούνται κατά την αναβάθμιση και ο installer κρατά αντίγραφο επαναφοράς του προηγούμενου plugin.

1. Κατεβάστε από τα [Releases](../../releases/latest) το `OpsisNewsCrawler-Setup.exe` (ή το `…-portable.zip`) και το αντίστοιχο `.sha256`.
2. **Ελέγξτε το αρχείο** (PowerShell, στον φάκελο λήψεων):

   ```powershell
   (Get-FileHash .\OpsisNewsCrawler-Setup.exe -Algorithm SHA256).Hash.ToLower()
   Get-Content .\OpsisNewsCrawler-Setup.exe.sha256
   ```
   Οι δύο γραμμές πρέπει να ξεκινούν με το ίδιο 64ψήφιο αποτέλεσμα. Αν διαφέρουν, μην τρέξετε το αρχείο.
3. Τρέξτε τον installer (ζητά δικαιώματα διαχειριστή: το πρόγραμμα μπαίνει στο Program Files, οι ρυθμίσεις μένουν στο `%AppData%\OpsisNewsCrawler`· μια παλιότερη εγκατάσταση ανά χρήστη αφαιρείται αυτόματα). Ο οδηγός ρωτά για τρόπο εγκατάστασης (plugin του Opsis ή ξεχωριστή εφαρμογή), προεπιλεγμένη μπάρα και εξαρτήσεις.

Φορητή έκδοση (`…-portable.zip`): αποσυμπιέστε και τρέξτε `OpsisNewsCrawler.exe` (χωρίς plugin του Opsis· οι ρυθμίσεις μένουν στο `%AppData%\OpsisNewsCrawler`).

Σιωπηλή εγκατάσταση: `OpsisNewsCrawler-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /MODE=separate|integrated /BAR=above|taskbar /DEPS=all|dotnet|webview2|none` (το `/DEPS` είναι `none` αν δεν δοθεί).

> Οι installers δεν είναι ψηφιακά υπογεγραμμένοι· τα Windows SmartScreen μπορεί να προειδοποιήσουν. Γι' αυτό υπάρχει το SHA-256.

### Απόρρητο και δίκτυο

Το πρόγραμμα δεν στέλνει τηλεμετρία. Εκτός από τις πηγές ειδήσεων/τιμών/καιρού που επιλέγετε, κάνει μόνο: (1) έναν έλεγχο νέας έκδοσης προς το GitHub (`api.github.com`, 30 s μετά την εκκίνηση και μία φορά την ημέρα, με ETag) και (2) όταν χρησιμοποιείτε φυσικές καταστροφές (USGS, EMSC, GDACS, NASA EONET: ένα GET ανά πηγή κάθε 15 λεπτά, μόνο όσο εμφανίζονται ή τροφοδοτούν το εικονίδιο), (3) όταν πατήσετε Αναζήτηση τόπου (Open-Meteo geocoding· OpenStreetMap Nominatim για ταχυδρομικούς κώδικες και συντεταγμένες, το πολύ ένα αίτημα το δευτερόλεπτο) και (4) όταν ανοίξετε το παράθυρο «Η οικογένεια Opsis», τη λήψη του καταλόγου εφαρμογών (`raw.githubusercontent.com`, το πολύ μία φορά την ημέρα) και την ερώτηση για την τελευταία έκδοση κάθε εγκατεστημένης εφαρμογής του καταλόγου. Κανένα αρχείο δεν κατεβαίνει χωρίς τη δική σας επιβεβαίωση και κανένα δεν εκτελείται από το πρόγραμμα. Το κλειδί Anthropic του προαιρετικού βοηθού αποθηκεύεται κρυπτογραφημένο (DPAPI) και δεν εξάγεται.

### Πηγές δεδομένων, πιστώσεις και άδειες τρίτων

Ειδήσεις: RSS των ίδιων των εκδοτών (μόνο τίτλος, σύντομη περίληψη και σύνδεσμος). Τιμές: Euronext, gold-api.com, Frankfurter/ΕΚΤ, FRED/EIA. Καιρός: [Open-Meteo.com](https://open-meteo.com) (CC BY 4.0) και, μόνο για τόπους που δεν απαντά το Open-Meteo, [MET Norway](https://api.met.no) (CC BY 4.0). Σεισμοί και καταστροφές: [USGS](https://earthquake.usgs.gov), [EMSC](https://www.emsc-csem.org), [GDACS](https://www.gdacs.org), [NASA EONET](https://eonet.gsfc.nasa.gov). Χάρτης και αναζήτηση τόπων: © συνεργάτες του [OpenStreetMap](https://www.openstreetmap.org/copyright) (ODbL). Οι τιμές δεν αποτελούν επενδυτική συμβουλή. Λεπτομέρειες και όροι: [`docs/DATA-SOURCES.md`](docs/DATA-SOURCES.md).

Βιβλιοθήκες: [Microsoft.Web.WebView2](https://www.nuget.org/packages/Microsoft.Web.WebView2) (Microsoft, άδεια BSD-style της Microsoft), για τα tests xUnit (Apache-2.0). Οι ιδέες του χαρτοφυλακίου προέρχονται από το [stonks-cli](https://github.com/igoropaniuk/stonks-cli) (MIT) και του RSI/SMA από το [greek-stocks-ai](https://github.com/karidasd/greek-stocks-ai)· ο κώδικας γράφτηκε από την αρχή. Ο installer χρησιμοποιεί το [Inno Setup](https://jrsoftware.org/isinfo.php).

### Χτίσιμο από τον πηγαίο κώδικα

```
dotnet build OpsisNewsCrawler.sln -c Release
dotnet test  OpsisNewsCrawler.sln -c Release
powershell -ExecutionPolicy Bypass -File tools\release.ps1     # publish + installer + portable + SHA-256 στο release\<έκδοση>-assets
```

Χρειάζεται .NET 11 SDK και Inno Setup 6/7. Δομή: `src/OpsisNewsCrawler.Core` (μοντέλα, ρυθμίσεις, RSS, τιμές, καιρός, γλώσσες), `src/OpsisNewsCrawler.Windows` (WinForms UI, Win32), `src/OpsisNewsCrawler.App` (αυτόνομη εφαρμογή), `src/OpsisNewsCrawler.OpsisPlugin` + `…Plugin.Abstractions` (plugin του Opsis), `tests/` (xUnit), `installer/`, `tools/`, `docs/`.

---

## English

A scrolling news, market-price and weather bar for the Windows desktop — above the taskbar, **inside** it, or in the middle of the screen. It runs on its own (notification-area icon) or as a plugin of **Opsis - Desktop Customization**. Free and open source (MIT).

<p align="center"><img src="docs/screenshots/bar-news-1.png" width="900" alt="the bar"/><br/><img src="docs/screenshots/bar-news-2.png" width="900" alt="the bar"/><br/><img src="docs/screenshots/bar-hazards-weather.png" width="900" alt="natural disasters and weather"/></p>

### Features

| What | Description |
|---|---|
| 📰 News | RSS from many sources in **6 categories** (Economy, Markets, International, Greece, Defence & Diplomacy, Sports) plus your own; the same story from several sources is shown once; breaking news with a red bar, sound and a live-video link; "read later" with Ctrl+click |
| 📈 Prices | Euronext shares (Athens 15′ delayed, Paris, Amsterdam, Brussels, Lisbon, Milan, Oslo), metals, oil/gas, ECB currency rates; price alerts; quotes are labelled delayed |
| 💼 Portfolio and analysis | Value and profit/loss, RSI, SMA20/50, MACD, candlesticks, strategy back-test, fear & greed gauge, news mood |
| 🗂 Topics and stories | Topic watchlists (keywords and region), stories grouped by event with a source comparison (not a fact check), a local timeline shared with the Opsis apps |
| 🔎 Search and focus | Search the news, a keyboard focus reading mode, two reports side by side, emerging terms, a story timeline and hints on differing numbers, shared origin and country |
| 📊 Analytics | People and relations, coverage, trends, a world map, a market heatmap, volatility and drawdown, allocation and time-weighted return, a dividend and indicator calendar, an optional local news archive (not investment advice) |
| 📅 Chronikologio without Opsis | The On this day flow reaches the bars when the two programs run separately |
| 📤 Exports | Daily and weekly HTML digests (offline, safely encoded, print to PDF from the browser), an economic calendar with filters (JSON import, ICS export), an M3U playlist for Opsis Media Suite |
| 🧾 Portfolio ledger | Buys, sells, dividends, splits and fees per currency (no automatic conversion), weighted average cost, realised profit/loss, valuation from the last known price; old holdings move over as an opening balance after confirmation and come back if saving fails |
| 🔔 Price alerts | Price or daily change %, re-arm margin, maximum data age; zero or missing prices never trigger them |
| 📺 Live Video Hub | Optional: channels of your choice, muted, never plays by itself |
| 🧱 Opsis widgets | Five optional widgets (news, markets, weather, urgent, history), a news studio and gallery, high contrast, keyboard navigation |
| 🛡 Stability and security | Limits on parallel downloads, back-off and Retry-After, 5 MB cap per feed, redirect guard (never to local addresses, never from https to http), safe mode, fewer disk writes, installer with a rollback copy |
| ⛅ Weather | Greek cities: icon, temperature, wind, humidity; map on click; severe-weather warnings |
| 🌋 Natural disasters | Earthquakes, fires, floods, storms, volcanoes as bar content; filters for region, magnitude, age and kind; reports of several organisations merged |
| 🖥 Bars | Above the taskbar or **inside** it (follows auto-hide); a second bar, a bar per monitor, a desktop bar that stands in for a hidden taskbar; each bar shows news, markets or weather |
| 🎨 Look | Themes (the same as Opsis), colour, transparency, background material (acrylic/blur), fading ends; fully keyboard-operable controls |
| 🧩 Plugin or stand-alone | Integrated into Opsis (its own page, Opsis language and colours) or a separate app with a notification-area icon |
| 🌍 14 languages | Greek, English, German, French, Spanish, Italian, Portuguese, Russian, Turkish, Japanese, Korean, Simplified Chinese, Arabic (right-to-left), Hindi |

**New in 0.16.1:** "Disasters flow" page, a separate speed per bar and per half of a split bar, a Yahoo catalogue with 100+ instruments to pick from, the same card width in every language.

**New in 0.16.0:** **natural disasters** (earthquakes, fires, floods, storms, volcanoes from USGS, EMSC, GDACS, NASA EONET; the same earthquake from several organisations shows the average magnitude and the range), **weather for any place** (name, postcode or coordinates), the animated icon shows the current weather and the highest active alert, mouse wheel, oil and gas from Yahoo futures (optional).

**New in 0.15.0:** welcome guide and "The Opsis family" window with the animated icon (newspaper, ball, shield, economy, weather, natural disasters), one update notice per version, rectangular icon, Opsis themes, correct auto-hide taskbar detection, full keyboard support in the custom controls. Full history: [`CHANGELOG.md`](CHANGELOG.md).

**Additional P1 settings:** The News widget can use one existing RSS category chosen manually (for example Greece or International), without determining your location. The Markets widget can show optional financial cards with the last available reported value, which may be delayed. Neither option adds a new market/feed provider or background daemon.

**P1 (development preview, not yet released):** Settings → Widget Studio customizes the five existing Opsis widgets: layout, up to 30 rows, 80–160% text size, optional publisher/timestamp and host accent. News, Breaking and On This Day support opt-in **Newsroom Gallery** cards. Five one-click settings presets include a larger-text Easy Read mode; Opsis remains responsible for monitor placement and widget lifecycle.

### Screenshots

Dark and light themes (all of them: [`docs/screenshots`](docs/screenshots)).

<table>
<tr>
<td><img src="docs/screenshots/en/dark/02-bars.png" width="440"/><br/><sub>Bars (dark)</sub></td>
<td><img src="docs/screenshots/en/light/05-news.png" width="440"/><br/><sub>News flow (light)</sub></td>
</tr>
<tr>
<td><img src="docs/screenshots/en/dark/06-markets.png" width="440"/><br/><sub>Markets</sub></td>
<td><img src="docs/screenshots/en/light/07-weather.png" width="440"/><br/><sub>Weather</sub></td>
</tr>
<tr>
<td><img src="docs/screenshots/welcome/welcome-en-1.png" width="440"/><br/><sub>Welcome guide</sub></td>
<td><img src="docs/screenshots/welcome/family-en.png" width="440"/><br/><sub>The Opsis family</sub></td>
</tr>
<tr>
<td><img src="docs/screenshots/en/light/09-analysis-candles.png" width="440"/><br/><sub>Market analysis</sub></td>
<td><img src="docs/screenshots/en/dark/03-look.png" width="440"/><br/><sub>Look</sub></td>
</tr>
<tr>
<td><img src="docs/screenshots/en/light/10-weather-search.png" width="440"/><br/><sub>Weather: find any place</sub></td>
<td><img src="docs/screenshots/en/light/11-disasters.png" width="440"/><br/><sub>Disasters flow</sub></td>
</tr>
</table>

### Installation

**Requirements:** Windows 11 (64-bit; Windows 10 is untested), the [.NET 11 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/11.0) (the installer offers it and downloads it only if you allow), optionally the WebView2 Runtime (live video of breaking news).

**0.28.0 compatibility:** as a plugin it needs **Opsis 2.8 or 3.0** (shared plugin contract 1.0.0.0, exactly the same DLL as the host, and Opsis.UI 0.6.0); the installer refuses to integrate when the host's DLL differs and installs stand-alone instead. Every app of the family runs on .NET 11. Your settings are kept on upgrade and the installer keeps a rollback copy of the previous plugin.

1. Download `OpsisNewsCrawler-Setup.exe` (or `…-portable.zip`) and the matching `.sha256` from [Releases](../../releases/latest).
2. **Verify the file** (PowerShell, in the download folder):

   ```powershell
   (Get-FileHash .\OpsisNewsCrawler-Setup.exe -Algorithm SHA256).Hash.ToLower()
   Get-Content .\OpsisNewsCrawler-Setup.exe.sha256
   ```
   Both lines must start with the same 64-digit value. If they differ, do not run the file.
3. Run the installer (it asks for administrator rights: the program goes to Program Files, the settings stay in `%AppData%\OpsisNewsCrawler`; an earlier per-user installation is removed automatically). The wizard asks how to install (Opsis plugin or separate app), the default bar and the dependencies.

Portable (`…-portable.zip`): unzip and run `OpsisNewsCrawler.exe` (no Opsis plugin; settings stay in `%AppData%\OpsisNewsCrawler`).

Silent install: `OpsisNewsCrawler-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /MODE=separate|integrated /BAR=above|taskbar /DEPS=all|dotnet|webview2|none` (`/DEPS` defaults to `none`).

> The installers are not code-signed; Windows SmartScreen may warn. That is what the SHA-256 is for.

### Privacy and network

The program sends no telemetry. Besides the news/price/weather sources you choose, it only does: (1) one version check to GitHub (`api.github.com`, 30 s after the start and once a day, with an ETag) and (2) when natural disasters are used (USGS, EMSC, GDACS, NASA EONET: one GET per source every 15 minutes, only while a bar shows them or the icon uses them), (3) when you press Search for a place (Open-Meteo geocoding; OpenStreetMap Nominatim for postcodes and coordinates, at most one request a second) and (4) when you open "The Opsis family" window, one download of the app catalogue (`raw.githubusercontent.com`, at most once a day) and one question about the latest release of each installed app of the catalogue. No file is downloaded without your confirmation and the program never runs one. The Anthropic key of the optional assistant is stored encrypted (DPAPI) and never exported.

### Data sources, credits and third-party licences

News: the publishers' own RSS feeds (only the title, a short summary and the link). Prices: Euronext, gold-api.com, Frankfurter/ECB, FRED/EIA. Weather: [Open-Meteo.com](https://open-meteo.com) (CC BY 4.0) and, only for places Open-Meteo does not answer for, [MET Norway](https://api.met.no) (CC BY 4.0). Earthquakes and disasters: [USGS](https://earthquake.usgs.gov), [EMSC](https://www.emsc-csem.org), [GDACS](https://www.gdacs.org), [NASA EONET](https://eonet.gsfc.nasa.gov). Map and place search: © [OpenStreetMap](https://www.openstreetmap.org/copyright) contributors (ODbL). Prices are not investment advice. Details and terms: [`docs/DATA-SOURCES.md`](docs/DATA-SOURCES.md).

Libraries: [Microsoft.Web.WebView2](https://www.nuget.org/packages/Microsoft.Web.WebView2) (Microsoft licence), xUnit for the tests (Apache-2.0). The portfolio idea comes from [stonks-cli](https://github.com/igoropaniuk/stonks-cli) (MIT) and the RSI/SMA idea from [greek-stocks-ai](https://github.com/karidasd/greek-stocks-ai); the code was written from scratch. The installer is built with [Inno Setup](https://jrsoftware.org/isinfo.php).

### Building from source

```
dotnet build OpsisNewsCrawler.sln -c Release
dotnet test  OpsisNewsCrawler.sln -c Release
powershell -ExecutionPolicy Bypass -File tools\release.ps1     # publish + installer + portable + SHA-256 into release\<version>-assets
```

Needs the .NET 11 SDK and Inno Setup 6/7. Layout: `src/OpsisNewsCrawler.Core` (models, settings, RSS, prices, weather, languages), `src/OpsisNewsCrawler.Windows` (WinForms UI, Win32), `src/OpsisNewsCrawler.App` (stand-alone app), `src/OpsisNewsCrawler.OpsisPlugin` + `…Plugin.Abstractions` (Opsis plugin), `tests/` (xUnit), `installer/`, `tools/`, `docs/`.

---

## Η οικογένεια Opsis / The Opsis family

Ο ίδιος δημιουργός, εφαρμογές για Windows / apps for Windows by the same author (το παράθυρο «Η οικογένεια Opsis» μέσα στην εφαρμογή δείχνει ποιες είναι εγκατεστημένες / the "Opsis family" window inside the app shows which are installed):

| App | |
|---|---|
| [Opsis](https://github.com/gs-applabs/Opsis) | Desktop customisation for Windows 11 — wallpapers, widgets, DeskStrip, plugins / Εξατομίκευση επιφάνειας εργασίας |
| [Opsis News Crawler](https://github.com/gs-applabs/OpsisNewsCrawler) | News and markets ticker inside your taskbar (**this repository**) |
| [GearWin](https://github.com/gs-applabs/GearWin) | Complete PC care for Windows 11 |
| [Opsis Media Suite](https://github.com/gs-applabs/OpsisMediaSuite) | Video and audio player, converter and photo tools |
| [Kombos](https://github.com/gs-applabs/Kombos) | A local hub for AI tools, with Claude as the agent |

## Άδεια / License

[MIT](LICENSE) © 2026 gs-applabs

Ελληνικά: ελεύθερη χρήση, αντιγραφή, τροποποίηση και διανομή, με την προϋπόθεση να διατηρείται η σημείωση πνευματικών δικαιωμάτων και η άδεια· το λογισμικό παρέχεται «ως έχει», χωρίς εγγύηση. Εκδότης όλων των αρχείων: gs-applabs.

English: you may use, copy, modify and distribute the program as long as the copyright notice and the licence text are kept; it is provided "as is", without warranty. Publisher of all files: gs-applabs.
