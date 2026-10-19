from pathlib import Path
from io import BytesIO
from PIL import Image
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib.colors import HexColor
from reportlab.lib.utils import ImageReader
from pypdf import PdfReader

ROOT=Path(__file__).resolve().parent
pdfmetrics.registerFont(TTFont('J','C:/Windows/Fonts/meiryo.ttc',subfontIndex=0))
pdfmetrics.registerFont(TTFont('B','C:/Windows/Fonts/meiryob.ttc',subfontIndex=0))
W,H=1280,720
INK='#293252';PINK='#F5529C';BLUE='#40C7EB';YELLOW='#FFDD63';GREEN='#BAE986';BG='#FFFBF1';MUTED='#59627C'
c=canvas.Canvas(str(ROOT/'Roomies_中間発表.pdf'),pagesize=(W,H))
c.setTitle('Roomies | 卒業研究 中間発表');c.setAuthor('西宮優正 (TK230088)')
def box(x,y,w,h,col,r=22,stroke=False):
    c.setFillColor(HexColor(col));c.setStrokeColor(HexColor(INK));c.setLineWidth(2)
    c.roundRect(x,H-y-h,w,h,r,fill=1,stroke=int(stroke))
def txt(s,x,y,size=24,col=INK,bold=False):
    c.setFont('B' if bold else 'J',size);c.setFillColor(HexColor(col));c.drawString(x,H-y-size*.9,s)
def lines(ss,x,y,size=23,gap=39,col=INK,bold=False):
    for i,s in enumerate(ss):txt(s,x,y+i*gap,size,col,bold)
def circle(x,y,r,col):
    c.setFillColor(HexColor(col));c.circle(x,H-y,r,fill=1,stroke=0)
def sticker(s,x,y,col=YELLOW,size=17):
    w=pdfmetrics.stringWidth(s,'B',size)+34;box(x+4,y+5,w,36,INK,14);box(x,y,w,36,col,14,True);txt(s,x+17,y+8,size,bold=True)
def card(x,y,w,h,col='#FFFFFF'):
    box(x+6,y+7,w,h,INK);box(x,y,w,h,col,stroke=True)
def photo(name,x,y,w,h,contain=False):
    im=Image.open(ROOT/'screenshots'/f'{name}.png').convert('RGB');iw,ih=im.size
    ratio=(min if contain else max)(w/iw,h/ih);dw,dh=iw*ratio,ih*ratio
    b=BytesIO();im.save(b,'JPEG',quality=92);b.seek(0)
    c.saveState();p=c.beginPath();p.roundRect(x,H-y-h,w,h,18);c.clipPath(p,stroke=0,fill=0)
    c.drawImage(ImageReader(b),x+(w-dw)/2,H-y-h+(h-dh)/2,dw,dh);c.restoreState()
def base(n,sec,title,sub='',bg=BG):
    box(0,0,W,H,bg,0);circle(1240,12,97,YELLOW);circle(1218,32,17,PINK)
    for i in range(4):circle(1145+i*24,86,4,INK)
    sticker(sec,44,27,BLUE,15);txt(title,44,93,37,bold=True)
    if sub:txt(sub,47,147,19,MUTED)
    txt('ROOMIES  /  卒業研究 中間発表  /  西宮 優正',46,686,11,MUTED)
    box(1150,672,86,32,YELLOW,16,True);txt(f'{n:02} / 12',1166,680,13,bold=True)
def cap(s,x,y):txt(s,x,y,12,MUTED)
def end():c.showPage()

# 01
box(0,0,W,H,'#FFE9F2',0);circle(1120,80,220,YELLOW);circle(30,660,160,BLUE)
photo('portfolio-hero',540,54,693,580);box(40,73,550,290,INK);box(32,62,550,290,'#FFFFFF',stroke=True)
sticker('卒業研究 中間発表',58,83,YELLOW)
txt('ROOMIES',52,137,78,PINK,True)
lines(['共同生活を通じて','コミュニケーションを促す','オンラインマルチプレイゲームの制作'],59,237,23,34,bold=True)
sticker('だれが稼ぐ？',758,482,BLUE,23);sticker('何に使う？',952,549,YELLOW,23)
lines(['東京国際工科専門職大学  工科学部','デジタルエンタテインメント学科'],52,405,21,35)
txt('TK230088  西宮 優正',52,499,28,bold=True)
txt('指導教員：根岸 豊 教授',53,548,19,MUTED)
cap('2026.09.09 / 開発中のゲーム素材による作品紹介用撮影',54,672);end()

# 02
base(2,'01 / 制作テーマ','「一緒に暮らす」が、会話のきっかけに','友人同士など、少人数で協力型ゲームを遊ぶプレイヤーに向けた作品。')
photo('reference-concept',46,203,570,369);cap('参考報告書 図1：コンセプトアート（生成AI使用）',48,588)
sticker('目指す体験',658,211,PINK)
lines(['生活の判断を共有することで、','自然な相談と役割分担が生まれる。'],658,276,27,47,bold=True)
for x,y,s,col in [(660,400,'「今日はどの仕事？」',BLUE),(803,469,'「家具を買っていい？」',YELLOW),(661,538,'「家賃までに足りる？」',GREEN)]:sticker(s,x,y,col,21)
end()

# 03
base(3,'02 / ゲーム概要','1〜5人で暮らす、一人称の共同生活ゲーム','アルバイトで稼ぎ、共同口座を管理し、3日ごとの家賃を支払う。')
data=[('仕事を選ぶ','得意なことを分担',BLUE),('共同口座へ','みんなの収入を共有',PINK),('家具・生活費','使い道を相談',YELLOW),('家賃を払う','生活を続ける',GREEN)]
for i,(a,b,col) in enumerate(data):
    x=46+i*304;card(x,241,274,218,col);txt(f'0{i+1}',x+22,261,36,bold=True);txt(a,x+22,326,28,bold=True);txt(b,x+22,391,19)
    if i<3:txt('→',x+278,334,24,bold=True)
card(46,509,1184,121);txt('完成時の最低到達点',73,530,22,PINK,True)
lines(['Steamから参加し、移動・仕事・資金管理・家賃支払いまで、','複数人で一連の生活を最後まで遊べる状態にする。'],375,533,23,38);end()

# 04
base(4,'03 / システム設計','同じ家・同じお金・同じ日付を共有する','Unity / Netcode for GameObjects / Steamworks のロビー・P2Pを利用。')
photo('story-roommates',46,210,582,328);cap('共有するリビング / 撮影用に配置した2人のルームメイト',46,550)
card(676,210,553,350)
sticker('Host / Client',701,226,BLUE)
lines(['操作する側：移動・インタラクト','ホスト側：報酬や購入などの結果を確定','共有する情報：口座・日付・家賃・仕事'],704,300,22,58)
box(46,588,1184,61,YELLOW,18,True);txt('統合時の課題：Host・Client・同時操作で、状態が一致するか。',70,606,25,bold=True);end()

# 05
base(5,'04 / 進捗ボード','できたことを土台に、次のタスクへ。','9月3日の報告書に基づく進捗。現在の統合ビルドでは再確認を進める。')
for x,title,col,items,done in [(46,'確認済み',GREEN,['Steam Host / Join','移動・インタラクト','共同口座・家賃','運び屋・牢屋への移送'],True),(653,'調整・検証中',YELLOW,['ブラックジャック・スロット','デリバリー・個人所持金','アニメーション','接続統合・通しプレイ'],False)]:
    card(x,211,575,410,'#EAF2E5' if done else '#FFF4DA')
    sticker(title,x+22,228,col,25)
    txt('DONE' if done else 'IN PROGRESS',x+365,241,16,MUTED,True)
    for i,label in enumerate(items):
        y=295+i*76;box(x+20,y,535,61,'#FFFFFF',13)
        box(x+36,y+18,25,25,col,6)
        if done:
            c.setStrokeColor(HexColor(INK));c.setLineWidth(2.5)
            c.line(x+41,H-y-30,x+47,H-y-36);c.line(x+47,H-y-36,x+57,H-y-24)
        else:
            circle(x+48.5,y+30.5,7,INK);circle(x+48.5,y+30.5,3.5,col)
        txt(label,x+78,y+18,23,bold=True)
cap('確認済み＝報告書に記載された実機確認。調整・検証中＝最終確認や統合が残る項目。',48,646);end()

# 06
base(6,'05 / その後の制作','仕事と買い物の選択肢を増やす','9月9日までの追加実装。通信を含む通し検証と人数別バランス調整は今後実施。')
items=[('portfolio-checkout','コンビニのレジ打ち',['商品をスキャンしてお釣りを選ぶ。','連続正解で報酬アップ。'],BLUE),('portfolio-ore','地下採掘',['地下3層で掘り、運び、売却する。','重さと日没が帰還の判断に。'],PINK),('portfolio-home','家具を買う楽しさ',['12種類の家具と生活・仕事の効果。','配置した家具が翌朝から役立つ。'],GREEN)]
for i,(im,t,ss,col) in enumerate(items):
    x=46+i*404;card(x,209,376,421);photo(im,x+10,219,356,217);sticker(t,x+17,451,col,21);lines(ss,x+19,515,18,34)
cap('開発中のゲーム画面 / 撮影用にカメラ・照明を調整',48,651);end()

# 07
base(7,'06 / キャラクター・背景・演出','暮らす街を、入室した瞬間から見せる','ロビーの背景にも現在のワールドを反映。制作した素材をゲーム全体へ統合。')
photo('story-shopping-street',46,208,787,443);card(864,208,365,443,'#E6F8FF')
sticker('現在の制作',886,228,BLUE)
lines(['Bobの色と黒い目','家・店舗・牢屋・街路','丘や高架などの遠景','素材の模様・UI調整','効果音と軽いBGM'],888,299,23,53)
cap('街路とコンビニ / 仕事場へ続く街を別アングルから撮影',47,655);end()

# 08
base(8,'07 / 制作体制','4人の専門を、ひとつのゲームへ','参考報告書に基づく担当。定期的に実装状況を共有し、統合できる状態を基準に進行。')
roles=[(46,214,'西宮 優正 / リーダー',['全体統括・プログラム','背景制作・ゲーム全体の統合'],PINK),(653,214,'サブリーダー / TA',['Bobのリグ','歩行・持つアニメーション'],BLUE),(46,427,'エフェクト担当',['演出・エフェクト制作','ゲーム内の各機能へ組み込み'],YELLOW),(653,427,'広報担当',['X運用・プロモーション','開発素材と制作過程の発信'],GREEN)]
for x,y,title,ss,col in roles:
    card(x,y,575,184);box(x+16,y+16,9,148,col,4);txt(title,x+42,y+24,26,bold=True);lines(ss,x+42,y+82,22,37)
end()

# 09
base(9,'08 / 進捗管理・広報','つくる過程も、チームで共有する','制作中から進捗を見えるようにし、作品と開発チームの認知を育てる。')
card(46,213,573,420);card(655,213,573,420)
photo('reference-lumina',65,229,242,272,True)
sticker('Lumina / Discord',327,233,BLUE,17)
lines(['タスク・担当・期限','優先度を整理','会話で進捗と','残課題を確認'],329,300,21,47)
photo('reference-promotion',675,229,534,266,True)
txt('Xで継続して発信',680,520,24,bold=True)
lines(['コンセプト・開発画面・制作過程を共有。','公式とメンバーのアカウントで広報。'],680,563,20,32)
cap('参考報告書 図3：タスク・進捗管理',65,602)
cap('画像：参考報告書 図3・図4より',47,651);end()

# 10
base(10,'09 / 今後の計画','まず通して遊べる状態へ。その後、磨く。','参考報告書の制作計画を軸に、先行実装した背景・音・UIは統合と品質調整を進める。')
for x,month,title,ss,col in [(46,'9月','接続と生活の安定化',['ブランチ統合・Host / Client検証','最初から家賃までの通しプレイ','歩行・持つアニメーション'],BLUE),(449,'10月','空間と演出の充実',['背景・家具の作り込み','エフェクトの制作・統合','遊びやすい導線へ調整'],PINK),(852,'11月以降','音・UI・最終仕上げ',['BGM・生活音SE・UIの品質向上','Steamで複数人プレイを検証','安定した提出用ビルドへ'],YELLOW)]:
    card(x,230,376,350);sticker(month,x+22,250,col,28);txt(title,x+22,331,25,bold=True);lines(ss,x+22,398,19,46)
box(46,610,1184,44,GREEN,15,True);txt('最終目標：友人同士で繰り返し遊べる、安定した共同生活体験。',68,622,22,bold=True);end()

# 11
base(11,'10 / 評価方法','「自然に相談したくなる」を、どう確かめる？','複数人で通しプレイを行い、行動の観察とプレイ後の簡易アンケートを組み合わせる。')
for x,title,ss,col in [(46,'観察する場面',['仕事を選ぶとき','共同資金を使うとき','家賃に向けて稼ぐとき'],BLUE),(450,'見たい行動',['自分から相談したか','役割分担が生まれたか','判断を共有できたか'],PINK),(854,'プレイ後に聞く',['相談したくなった場面は？','迷った操作や判断は？','友人とまた遊びたい？'],YELLOW)]:
    card(x,236,376,291);sticker(title,x+22,257,col,23);lines(ss,x+24,336,23,52)
card(46,564,1184,83,'#E4F6D7');txt('検証したいこと',68,583,21,bold=True);txt('生活の共有判断が、会話や役割分担のきっかけになっているか。',289,590,22)
end()

# 12
base(12,'11 / まとめ','一緒に暮らすから、一緒に考えたくなる。','Roomies：共同生活上の意思決定から、自然なコミュニケーションを生むゲームへ。', '#FFE9F2')
photo('story-doorstep',46,213,646,365);card(729,213,498,365)
sticker('これからの重点',751,235,YELLOW)
lines(['01  生活サイクルを安定させる','02  相談が生まれるか確かめる','03  見た目と操作感を磨く'],754,310,23,69,bold=True)
txt('ご意見・ご質問をお願いします！',46,605,31,PINK,True)
cap('内容参考：西宮優正「卒研 中間報告」（2026/9/3時点）。追加進捗：Roomies PROGRESS.md（2026/9/9）。',47,650)
end()
c.save()
r=PdfReader(str(ROOT/'Roomies_中間発表.pdf'));assert len(r.pages)==12
print('Created 12 pages')
