InnerFrame — B 配樂、互動音效、金色射線與選取框

適用：目前的 InnerFrame-Entrance 專案；Unity 6000.3.23f1 / XRI 3.3.2。

使用方式
1. 退出 Play Mode，匯入 InnerFrame_B_Letter_to_Vincent_Update.unitypackage，保留全部勾選項目。
2. 開啟原本 Assets/Entrance/Scenes/Entrance.unity，按 Play。
3. 配樂、音效與視覺回饋由原 EntranceMenu 自動建立，不需要手動接 AudioSource。
4. 若要使用頭盔，從原專案重新建置 Android APK。這個更新包不是 APK。

更新內容
• B：Letter to Vincent 配樂。開場／選房循環，Landing 1、梵谷字樣、金色過場與 Gallery 到達分段銜接。梵谷字樣後直接進金色，不再播放三張沉浸預覽圖。
• 金色前奏縮為 1.1 秒，場景淡出／淡入各 0.6 秒；金色與到達配樂以 0.3 秒交叉淡接，保持原播放速度。整段金色導入目標為 15 秒內，包含載入與連線等待；若場景本身載入超時，完成後直接顯示，不再額外等連線。金色邊緣提亮，並在釋放影片畫面前換上金色底圖。
• 所有 Entrance 按鈕，包括角色圖片、角色名稱、Back、Join、數字、DEL、Start、Skip，均由 onClick 觸發一次點擊聲。指向只顯示外框，不會一直發聲。
• Van Gogh 手寫聲跟隨原影片播放時間；略過影片會立即停止。
• 保留 XRI 的原射線互動，只修改顯示：金色線心、柔光及 UI 命中光點。
• 按鈕指向／鍵盤焦點顯示圓角黃色邊框；已選角色持續顯示外框。
• 配樂採獨立音軌，音效不會在選房等待時自行播放。

素材：原創配樂使用 GeneralUser GS 樂器取樣，授權見 Resources/Entrance/Audio/Soundfont-LICENSE.txt。
手寫素材：Stephan / PDSounds, Writing with inkpen，公有領域；剪輯、濾波及分段。
來源：https://commons.wikimedia.org/wiki/File:Writing_with_inkpen.ogg
原創按鈕聲由本次製作。
