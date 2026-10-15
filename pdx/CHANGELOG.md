# 0.5.2

* Maintenance has moved to [@kpgocek/parking-fee-control](https://github.com/kpgocek/parking-fee-control).
* Updated for game version 1.6.2f1.
* The **Update Frequency** option now works: fees are re-applied periodically, as configured.
* Parking buildings that the mod does not list (e.g. unsupported custom assets) are no longer forced to a $10 fee; their policies are left untouched.
* Out-of-range fees in `parking-config.json` are clamped to 0–50 instead of causing errors.
* Removed log spam when opening the fees panel.
