export const dict={
tr:{
welcome:"Hoş Geldiniz",ide:"Çalışma Alanı",agents:"Agent Studio",tasks:"Görev Merkezi",review:"Git ve İnceleme",intelligence:"Kod Zekâsı",approvals:"Onay Merkezi",models:"Model Studio",mcp:"MCP Yöneticisi",terminal:"Terminal ve Hata Ayıklama",browser:"Tarayıcı Otomasyonu",settings:"Ayarlar ve Temalar",recovery:"Kurtarma ve İzleme",
tagline:"Yazılım geliştirme için tasarlanmış akıllı çalışma alanı.",command:"Komut veya ekran ara...",newTask:"Yeni görev",prototype:"Arayüz prototipi",offline:"Sunucu bağlı değil",search:"Komut paleti",close:"Kapat",toggleAgent:"Agent panelini aç/kapat",toggleTerminal:"Terminali aç/kapat",
agentTitle:"AI Asistan",agentWelcome:"Güvenle geliştirin.",agentDescription:"Çoklu agent planlama, kontrollü kod değişikliği ve bağımsız inceleme.",agentAsk:"Asistanına sor...",send:"Gönder",agentOffline:"Backend bağlantısı yok. İstek gönderilmedi.",
welcomeTitle:"Projelerine kaldığın yerden devam et",openWorkspace:"Örnek çalışma alanını aç",recent:"Son projeler",restore:"Oturumu kurtar",restoreHelp:"Kesintiye uğrayan görevler ve kaydedilmemiş dosyalar doğrulanarak geri alınır.",
plan:"Plan",build:"Geliştir",test:"Test",verify:"İncele",approval:"Onay bekliyor",noConnection:"Canlı veri yok",notConfigured:"Yapılandırılmadı",notVerified:"Doğrulanmadı",noData:"Gösterilecek veri yok",
taskFlow:"Görev iş akışı",taskHelp:"Agent yürütme servisi henüz bağlı değil. Aşağıdaki adımlar tasarım örneğidir.",unknown:"Sonuç bilinmiyor",retryWarning:"Harici işlemler uzlaştırılmadan tekrar edilmez.",
theme:"Tema",dark:"Midnight Glass",light:"Pearl Light",density:"Yoğunluk",comfortable:"Rahat",compact:"Sıkı",focus:"Odak",language:"Arayüz dili",motion:"Animasyonları azalt",
panelMode:"Agent paneli",docked:"Sabit",floating:"Yüzer",hidden:"Gizli",pin:"Sabitle",unpin:"Sabiti kaldır",layoutReset:"Yerleşimi sıfırla",layoutPersisted:"Yerleşim bu tarayıcıda saklanır. Gizli bilgi kaydedilmez.",
modelTitle:"Model bağlantıları",localModel:"Yerel OpenAI uyumlu sunucu",toolCalling:"Tool calling",structured:"Yapılandırılmış çıktı",context:"Bağlam penceresi",
approvalTitle:"Bekleyen onay bulunmuyor",approvalHelp:"Risk, kaynak, süre ve izin kapsamı gerçek backend isteğinde gösterilir.",gitHelp:"Commit, push ve merge işlemleri kullanıcı kontrolündedir.",
recoveryTitle:"Kurtarma verisi bulunmuyor",recoveryHelp:"Checkpoint ve işlem günlükleri .NET servisinden doğrulanarak gösterilecek.",settingsInfo:"Görsel tercihleri ve yerleşimi düzenle",
codeTitle:"Editör önizlemesi",dirty:"Kaydedilmemiş değişiklik",mock:"Bu bölüm prototiptir; gerçek Git, dosya veya agent işlemi yapılmaz.",
fileTree:"DOSYALAR",openPalette:"Komutları aç",role:"Yetki profili",scope:"Yetki kapsamı",terminalTitle:"TERMINAL ÖNİZLEMESİ",
featureTitle:"Özellik önizlemesi",notConnected:"Bu özellik henüz gerçek servise bağlanmadı.",
logout:"Oturum kapalı",kbdHelp:"Esc kapatır · Enter seçer",pinned:"Sabitlendi"
},
en:{
welcome:"Welcome Hub",ide:"Workspace",agents:"Agent Studio",tasks:"Task Center",review:"Git & Review",intelligence:"Code Intelligence",approvals:"Approval Center",models:"Model Studio",mcp:"MCP Manager",terminal:"Terminal & Debugger",browser:"Browser Automation",settings:"Settings & Themes",recovery:"Recovery & Insights",
tagline:"An intelligent workspace designed for focused development.",command:"Search commands and screens...",newTask:"New task",prototype:"UI prototype",offline:"Backend not connected",search:"Command palette",close:"Close",toggleAgent:"Toggle agent panel",toggleTerminal:"Toggle terminal",
agentTitle:"AI Assistant",agentWelcome:"Build with confidence.",agentDescription:"Multi-agent planning, safe code edits and independent review.",agentAsk:"Ask your assistant...",send:"Send",agentOffline:"Backend unavailable. No request was sent.",
welcomeTitle:"Pick up where you left off",openWorkspace:"Open sample workspace",recent:"Recent projects",restore:"Restore session",restoreHelp:"Interrupted tasks and unsaved buffers require verified recovery.",
plan:"Plan",build:"Implement",test:"Test",verify:"Review",approval:"Awaiting approval",noConnection:"No live data",notConfigured:"Not configured",notVerified:"Unverified",noData:"Nothing to display",
taskFlow:"Task workflow",taskHelp:"Agent backend is not connected. Steps are design examples.",unknown:"Outcome unknown",retryWarning:"External operations must be reconciled before retry.",
theme:"Theme",dark:"Midnight Glass",light:"Pearl Light",density:"Density",comfortable:"Comfortable",compact:"Compact",focus:"Focus",language:"Interface language",motion:"Reduced motion",
panelMode:"Agent panel",docked:"Docked",floating:"Floating",hidden:"Hidden",pin:"Pin",unpin:"Unpin",layoutReset:"Reset layout",layoutPersisted:"Layout preferences are saved locally. Secrets are never stored.",
modelTitle:"Model connections",localModel:"Local OpenAI-compatible server",toolCalling:"Tool calling",structured:"Structured output",context:"Context window",
approvalTitle:"No pending approvals",approvalHelp:"Actual requests will display risk, resource, expiry and permission scope.",gitHelp:"Commit, push and merge remain user-controlled.",
recoveryTitle:"No recovery records",recoveryHelp:"Checkpoints and journals will be verified against the .NET Host.",settingsInfo:"Customize visuals and workspace layout",
codeTitle:"Editor preview",dirty:"Unsaved changes",mock:"Prototype only; no actual Git, filesystem or agent operation is executed.",
fileTree:"FILES",openPalette:"Open commands",role:"Permission profile",scope:"Approval scope",terminalTitle:"TERMINAL PREVIEW",
featureTitle:"Feature preview",notConnected:"This feature is not connected to a production service.",
logout:"Offline",kbdHelp:"Esc closes · Enter selects",pinned:"Pinned"
}
};
export function t(locale,key){const lang=locale==="EN"?"en":"tr";return dict[lang][key]??dict.en[key]??key;}
export function coverage(){const keys=Object.keys(dict.en);return {keys:keys.length,missing:keys.filter(k=>!(k in dict.tr)),extra:Object.keys(dict.tr).filter(k=>!(k in dict.en))};}