# WatchSourceFiles

Überwacht ein Verzeichnis inkl. Unterverzeichnissen auf Erstellung und Änderung von mehreren Dateitypen

Ich übernehme keine Gewähr oder Garantie für die Aktualität, Richtigkeit, Vollständigkeit und evtlle. Schäden.
Der Code darf frei übernommen und verändert werden.

Verwendet .Net Framework 4.8

Das Programm muss als Administrator ausgeführt werden,
um den Zugriff auf das zu überwachende Verzeichnis zu gewährleisten.

Beispiel Programmaufruf:
WatchSourceFiles.exe _.vb _.cs /watchdir:"c:\temp" /outdir:"c:\files"

[Filter] z.B. _.vb _.cs
Filter sind optional und werden Leerzeichen-getrennt und mit _ beginnend aufgelistet.
Sind keine Filter angegeben, werden die Filter '_.vb _.cs _.dll' verwendet.
[/watchdir:<Verzeichnis>]
Das zu überwachende Verzeichnis ist optional.
Ist es nicht angegeben, wird das Verzeichnis verwendet, in dem sich das Programm befindet.
[/outdir:<Verzeichnis>]
Das Ausgabeverzeichnis, in das die gefundenen Dateien kopiert werden, ist optional.
Ist es nicht angegeben, wird das Verzeichnis '%programdata%\MM\CopiedSourceFiles' verwendet.
