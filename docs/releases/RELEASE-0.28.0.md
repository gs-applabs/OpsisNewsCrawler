# Opsis News Crawler 0.28.0 — P0 + P1 + P2

**Product version:** 0.28.0  
**Crawler plugin:** `OpsisNewsCrawler.OpsisPlugin.dll`, AssemblyVersion / FileVersion **1.0.0.0**  
**Shared Opsis/Chronikologio ABI:** `OpsisNewsCrawler.Plugin.Abstractions.dll`, AssemblyVersion **0.8.0.0** (intentionally unchanged).

## Ελληνικά

### Ασφάλεια και απόδοση
- RSS: ταυτόχρονες λήψεις 6 συνολικά και 2 ανά host, όριο απάντησης 5 MB, σταδιακές επαναλήψεις, διάγνωση υγείας πηγών.
- Ακύρωση εργασιών κατά τον τερματισμό, λιγότερες άσκοπες εγγραφές SSD, μετρήσεις CPU/RAM/GDI/FPS/δικτύου, δυνατότητα σύνδεσης με χρέωση δεδομένων.
- Διορθώσεις ταυτόχρονης σχεδίασης του ticker, μερική επανασχεδίαση όπου είναι ασφαλής.

### Νέο περιβάλλον εργασίας / Opsis UI
- Βαθμίδες **Απλό / Προχωρημένο / Expert**, χωρίς απώλεια των υπάρχουσων ρυθμίσεων.
- Σχεδιαστής περιεχομένου μπάρας με drag-and-drop και κουμπί ανταλλαγής.
- Οπτικά προφίλ **Συγκέντρωση / Ειδησεογραφία / Υψηλή αντίθεση**, με τα κοινά στοιχεία Opsis UI.
- Υψηλό DPI, χειρισμός με πληκτρολόγιο, καλύτερη αντίθεση σε κινούμενα/φωτεινά φόντα.
- Προεπισκόπηση ειδήσεων με τις αναγνωρισμένες διαφορετικές εκδοτικές ομάδες, χωρίς ισχυρισμό ανεξάρτητης επαλήθευσης.
- **Κέντρο δραστηριότητας και χρονολόγιο** με φίλτρα κατηγορίας/ημερομηνίας και εξαγωγή JSON· τοπικά έως 500 στοιχεία, ελεγχόμενη αποθήκευση ανά πέντε λεπτά και κατά το κλείσιμο.

### Βίντεο και εφαρμογές Opsis
- **Live Video Hub** έκτακτων ειδήσεων: εναλλαγή ανάμεσα σε πηγές που έχει ήδη ορίσει ο χρήστης, προτίμηση με λέξεις-κλειδιά και περιοχή, χωρίς αυτόματο άνοιγμα.
- Πέντε πρόσθετα widgets για ειδήσεις, αγορές, καιρό, έκτακτες και ιστορική ροή. Για την εμφάνισή τους απαιτείται το συμβατό PR του Opsis.
- Το Χρονικολόγιο έχει χωριστό προαιρετικό Rich Feed bridge (χώρα, ιστορικό έτος, τύπο εγγραφής, μοναδικό κλειδί). **Δεν ενεργοποιείται σε υπάρχουσες εγκαταστάσεις** μέχρι να δοκιμαστεί η ίδια φυσική shared DLL σε όλα τα plugins.

## English

The release combines hardened RSS, low-write caching, event-aware breaking-news video with user-selected channels, the Opsis-style 3-level settings UI, bar drag-and-swap, visual presets, accessible/dpi-aware ticker and video popup, richer article source information, five optional host widgets and a locally persisted activity timeline.

### Compatibility and release safety

- Product `0.28.0` and plugin binary identity `1.0.0.0` are different version dimensions. They are intentionally not forced to match.
- The host's shared contract DLL stays at assembly identity `0.8.0.0` to avoid breaking already installed plugins.
- This release must pass Windows Core + Plugin tests, source/snapshot audit, installer build and isolated installation/upgrade/uninstall smoke checks before being marked stable.
- Signing requires a real Authenticode certificate; SHA-256 files are integrity aids and are not publisher signatures.
