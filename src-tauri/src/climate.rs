//! Protocol-independent climate controls. A vendor or Matter driver must supply
//! verified capabilities and perform the actual command; discovery alone never does.
use crate::model::ClimateState;

#[derive(Clone, Debug, PartialEq)]
pub enum ClimateCommand {
    Power(bool),
    TargetTemperature(f32),
    Mode(String),
    Fan(String),
}

pub fn validate(state: &ClimateState, command: ClimateCommand) -> Result<ClimateCommand, String> {
    let feature = match &command {
        ClimateCommand::Power(_) => "power",
        ClimateCommand::TargetTemperature(_) => "target_temperature",
        ClimateCommand::Mode(_) => "mode",
        ClimateCommand::Fan(_) => "fan_speed",
    };
    if !state.support.iter().any(|s| s == feature) {
        return Err("Bu klima seçilen özelliği desteklemiyor.".into());
    }
    match &command {
        ClimateCommand::TargetTemperature(value)
            if !value.is_finite() || *value < state.min_c || *value > state.max_c =>
        {
            return Err("Hedef sıcaklık desteklenen aralığın dışında.".into());
        }
        ClimateCommand::Mode(mode) if !state.modes.contains(mode) => {
            return Err("Bu çalışma modu desteklenmiyor.".into());
        }
        ClimateCommand::Fan(fan) if !state.fan_modes.contains(fan) => {
            return Err("Bu fan hızı desteklenmiyor.".into());
        }
        _ => {}
    }
    Ok(command)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn only_advertised_climate_controls_are_accepted() {
        let state = ClimateState {
            power: false,
            target_c: 24.0,
            ambient_c: None,
            mode: "cool".into(),
            fan: "auto".into(),
            min_c: 16.0,
            max_c: 30.0,
            modes: vec!["cool".into(), "fan".into()],
            fan_modes: vec!["auto".into(), "high".into()],
            support: vec!["power".into(), "target_temperature".into(), "mode".into()],
        };
        assert!(validate(&state, ClimateCommand::Power(true)).is_ok());
        assert!(validate(&state, ClimateCommand::TargetTemperature(22.5)).is_ok());
        assert!(validate(&state, ClimateCommand::TargetTemperature(31.0)).is_err());
        assert!(validate(&state, ClimateCommand::TargetTemperature(f32::NAN)).is_err());
        assert!(validate(&state, ClimateCommand::Mode("heat".into())).is_err());
        assert!(validate(&state, ClimateCommand::Fan("high".into())).is_err());
    }
}
