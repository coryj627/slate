// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Source identity for frontmatter keys (#1080). Display names are not
//! identifiers: YAML `1` and `"1"` may occur in the same mapping.

use serde::{Deserialize, Serialize};
use yaml_rust2::Yaml;

use crate::frontmatter::FrontmatterEditError;

const PREFIX: &str = "\u{f8ff}slate-key/v1:";
const NAMESPACE: &str = "\u{f8ff}slate-key/";

/// Source scalar type selected by an exact-key rename caller.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum PropertyKeyType {
    String,
    Integer,
    Boolean,
    Null,
    Real,
}

/// Resolve an explicitly selected scalar type in core, not in host parsers.
pub fn property_key_identity_for_type(
    name: &str,
    kind: PropertyKeyType,
) -> Result<String, FrontmatterEditError> {
    if kind == PropertyKeyType::String {
        return Ok(string_property_key_identity(name));
    }
    let yaml = if kind == PropertyKeyType::Null && name.is_empty() {
        Yaml::Null
    } else {
        Yaml::from_str(name)
    };
    if !matches!(
        (&yaml, kind),
        (Yaml::Integer(_), PropertyKeyType::Integer)
            | (Yaml::Boolean(_), PropertyKeyType::Boolean)
            | (Yaml::Null, PropertyKeyType::Null)
            | (Yaml::Real(_), PropertyKeyType::Real)
    ) {
        return Err(invalid(
            "the old key does not match the selected YAML key type",
        ));
    }
    Ok(yaml_key_identity(&yaml))
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type", content = "value", deny_unknown_fields)]
enum Key {
    String(String),
    Integer(i64),
    Boolean(bool),
    Null,
    Real(String),
    Path(Vec<Key>),
    Unsupported(String),
}

impl Key {
    fn from_yaml(key: &Yaml) -> Self {
        match key {
            Yaml::String(s) => Self::String(s.clone()),
            Yaml::Integer(n) => Self::Integer(*n),
            Yaml::Boolean(b) => Self::Boolean(*b),
            Yaml::Null => Self::Null,
            Yaml::Real(s) => Self::Real(s.clone()),
            other => Self::Unsupported(format!("{other:?}")),
        }
    }

    fn encode(&self) -> String {
        if let Self::String(s) = self
            && !s.starts_with(NAMESPACE)
        {
            return s.clone();
        }
        format!(
            "{PREFIX}{}",
            serde_json::to_string(self).expect("key serialization")
        )
    }

    fn display(&self) -> String {
        match self {
            Self::String(s) | Self::Real(s) | Self::Unsupported(s) => s.clone(),
            Self::Integer(n) => n.to_string(),
            Self::Boolean(b) => b.to_string(),
            Self::Null => "null".into(),
            Self::Path(parts) => parts
                .iter()
                .map(Self::display)
                .collect::<Vec<_>>()
                .join("."),
        }
    }
}

fn decode(identity: &str) -> Result<Key, FrontmatterEditError> {
    match identity.strip_prefix(PREFIX) {
        None => Ok(Key::String(identity.to_string())),
        Some(json) => {
            serde_json::from_str(json).map_err(|_| invalid("invalid property key identity"))
        }
    }
}

fn invalid(reason: &str) -> FrontmatterEditError {
    FrontmatterEditError::InvalidPropertyValue {
        reason: reason.to_string(),
    }
}

/// Mint an identity for an explicitly string-valued key (including names
/// that resemble our private token format). New-property callers use this.
pub fn string_property_key_identity(key: &str) -> String {
    Key::String(key.to_string()).encode()
}

pub(crate) fn yaml_key_identity(key: &Yaml) -> String {
    Key::from_yaml(key).encode()
}

pub(crate) fn unresolved_key_identity(key: &str) -> String {
    Key::Unsupported(key.to_string()).encode()
}

pub(crate) fn nested_key_identity(parent: &str, child: &Yaml) -> String {
    let parent = decode(parent).expect("parser-produced identity");
    let mut parts = match parent {
        Key::Path(parts) => parts,
        parent => vec![parent],
    };
    parts.push(Key::from_yaml(child));
    Key::Path(parts).encode()
}

/// Core-owned label for both visual and spoken property names. The type
/// suffix distinguishes typed keys from string keys with the same spelling.
pub fn property_key_label(identity: &str) -> String {
    let Ok(key) = decode(identity) else {
        return "Invalid property key".into();
    };
    let name = key.display();
    match key {
        Key::Integer(_) => format!("{name} (integer key)"),
        Key::Boolean(_) => format!("{name} (boolean key)"),
        Key::Null => "null (null key)".into(),
        Key::Real(_) => format!("{name} (number key)"),
        Key::Path(_) => format!("{name} (nested key)"),
        Key::Unsupported(_) => format!("{name} (unsupported key)"),
        Key::String(_) => name,
    }
}

pub(crate) fn property_key_display(identity: &str) -> Result<String, FrontmatterEditError> {
    Ok(decode(identity)?.display())
}

/// Decode only an editable top-level scalar. Reals retain yaml-rust2's
/// textual identity (1.0 and 1.00 are distinct in its mapping model).
pub(crate) fn property_key_yaml(identity: &str) -> Result<Yaml, FrontmatterEditError> {
    Ok(match decode(identity)? {
        Key::String(s) => Yaml::String(s),
        Key::Integer(n) => Yaml::Integer(n),
        Key::Boolean(b) => Yaml::Boolean(b),
        Key::Null => Yaml::Null,
        Key::Real(s) if Yaml::from_str(&s) == Yaml::Real(s.clone()) => Yaml::Real(s),
        _ => {
            return Err(invalid(
                "nested, unsupported or invalid property keys cannot be edited",
            ));
        }
    })
}
