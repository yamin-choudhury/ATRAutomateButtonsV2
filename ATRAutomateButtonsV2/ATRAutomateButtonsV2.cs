using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using System;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class AtrRiskBot : Robot
    {
        // Parameters for scale factor and percentage of account to risk
        [Parameter("Scale Factor", DefaultValue = 2.0)]
        public double ScaleFactor { get; set; }

        [Parameter("Risk Percent", DefaultValue = 1.0)]
        public double RiskPercent { get; set; }

        [Parameter("Volume Partition 1 (%)", DefaultValue = 60)]
        public double VolumePartition1 { get; set; }

        [Parameter("Volume Partition 2 (%)", DefaultValue = 15)]
        public double VolumePartition2 { get; set; }

        [Parameter("Volume Partition 3 (%)", DefaultValue = 15)]
        public double VolumePartition3 { get; set; }

        [Parameter("Volume Partition 4 (%)", DefaultValue = 10)]
        public double VolumePartition4 { get; set; }

        [Parameter("Take Profit 1 (pips)", DefaultValue = 30)]
        public double TP1 { get; set; }

        [Parameter("Take Profit 2 (pips)", DefaultValue = 50)]
        public double TP2 { get; set; }

        [Parameter("Take Profit 3 (pips)", DefaultValue = 100)]
        public double TP3 { get; set; }

        [Parameter("Take Profit 4 (pips)", DefaultValue = 0)]
        public double TP4 { get; set; }

        private StackPanel controlPanel;
        private Button buyButton;
        private Button sellButton;

        private AverageTrueRange atrM15;
        private AverageTrueRange atrH1;

        protected override void OnStart()
        {
            // Initialize ATR indicators for M15 and H1
            atrM15 = Indicators.AverageTrueRange(MarketData.GetSeries(TimeFrame.Minute15), 14, MovingAverageType.Simple);
            atrH1 = Indicators.AverageTrueRange(MarketData.GetSeries(TimeFrame.Hour), 14, MovingAverageType.Simple);

            // Create control panel
            controlPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = 10
            };

            // Create Buy button with ATR values
            buyButton = new Button
            {
                Text = $"Buy (ATR M15: {atrM15.Result.LastValue / Symbol.PipSize:F1} pips, H1: {atrH1.Result.LastValue / Symbol.PipSize:F1} pips)",
                BackgroundColor = Color.Green,
                Margin = 5
            };
            buyButton.Click += BuyButton_Click;

            // Create Sell button with ATR values
            sellButton = new Button
            {
                Text = $"Sell (ATR M15: {atrM15.Result.LastValue / Symbol.PipSize:F1} pips, H1: {atrH1.Result.LastValue / Symbol.PipSize:F1} pips)",
                BackgroundColor = Color.Red,
                Margin = 5
            };
            sellButton.Click += SellButton_Click;

            // Add buttons to the control panel
            controlPanel.AddChild(buyButton);
            controlPanel.AddChild(sellButton);

            // Add the control panel to the chart
            Chart.AddControl(controlPanel);
        }

        // Buy button click handler
        private void BuyButton_Click(ButtonClickEventArgs obj)
        {
            double[] volumePartitions = { VolumePartition1 / 100.0, VolumePartition2 / 100.0, VolumePartition3 / 100.0, VolumePartition4 / 100.0 };
            double[] tpValues = { TP1, TP2, TP3, TP4 };
            ExecuteMarketOrderWithPartitionsAsync(TradeType.Buy, volumePartitions, tpValues);
        }

        // Sell button click handler
        private void SellButton_Click(ButtonClickEventArgs obj)
        {
            double[] volumePartitions = { VolumePartition1 / 100.0, VolumePartition2 / 100.0, VolumePartition3 / 100.0, VolumePartition4 / 100.0 };
            double[] tpValues = { TP1, TP2, TP3, TP4 };
            ExecuteMarketOrderWithPartitionsAsync(TradeType.Sell, volumePartitions, tpValues);
        }

        // Asynchronous method for executing market orders
        private async void ExecuteMarketOrderWithPartitionsAsync(
            TradeType tradeType,
            double[] volumePartitions,
            double[] tpValues)
        {
            if (volumePartitions.Length != tpValues.Length)
            {
                Print("Error: The number of volume partitions must match the number of TP values.");
                return;
            }

            double atrValueM15 = atrM15.Result.LastValue;
            double stopLossPips = (atrValueM15 / Symbol.PipSize) * ScaleFactor;
            stopLossPips = Math.Round(stopLossPips, 1);

            double accountBalance = Account.Balance;
            double riskAmount = accountBalance * (RiskPercent / 100);
            double pipValue = Symbol.PipValue;
            double totalVolume = riskAmount / (stopLossPips * pipValue);
            totalVolume = Symbol.NormalizeVolumeInUnits(totalVolume);

            for (int i = 0; i < volumePartitions.Length; i++)
            {
                double partitionVolume = totalVolume * volumePartitions[i];
                partitionVolume = Symbol.NormalizeVolumeInUnits(partitionVolume);

                double stopLossPrice = tradeType == TradeType.Buy
                    ? Symbol.Bid - stopLossPips * Symbol.PipSize
                    : Symbol.Ask + stopLossPips * Symbol.PipSize;

                double takeProfitPrice = tradeType == TradeType.Buy
                    ? Symbol.Bid + tpValues[i] * Symbol.PipSize
                    : Symbol.Ask - tpValues[i] * Symbol.PipSize;

                Print("Partition Volume: {0}, TP: {1} pips", partitionVolume, tpValues[i]);

                // Execute the market order and get the TradeOperation result
                TradeOperation tradeOperation = ExecuteMarketOrderAsync(tradeType, SymbolName, partitionVolume, $"Partition {i + 1}", stopLossPips, tpValues[i] > 0 ? tpValues[i] : null);
            }

            // Optionally, await further processing here if needed
        }

        protected override void OnTick()
        {
            // Update the ATR values on the buttons
            buyButton.Text = $"Buy (ATR M15: {atrM15.Result.LastValue / Symbol.PipSize:F1} pips, H1: {atrH1.Result.LastValue / Symbol.PipSize:F1} pips)";
            sellButton.Text = $"Sell (ATR M15: {atrM15.Result.LastValue / Symbol.PipSize:F1} pips, H1: {atrH1.Result.LastValue / Symbol.PipSize:F1} pips)";
        }

        protected override void OnStop()
        {
            // Cleanup logic when the bot is stopped
            if (controlPanel != null)
            {
                Chart.RemoveControl(controlPanel);
            }
        }
    }
}
