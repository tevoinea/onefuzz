// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{collections::HashMap, path::PathBuf};

use crate::{
    local::common::{
        build_local_context, get_cmd_arg, get_cmd_exe, get_hash_map, get_synced_dir, CmdType,
        SyncCountDirMonitor, UiEvent, ANALYSIS_DIR, ANALYZER_ENV, ANALYZER_EXE, ANALYZER_OPTIONS,
        CRASHES_DIR, NO_REPRO_DIR, REPORTS_DIR, TARGET_ENV, TARGET_EXE, TARGET_OPTIONS, TOOLS_DIR,
        UNIQUE_REPORTS_DIR,
    },
    tasks::{
        analysis::generic::{run as run_analysis, Config},
        config::CommonConfig,
    },
};
use anyhow::Result;
use async_trait::async_trait;
use clap::{Arg, Command};
use flume::Sender;
use schemars::JsonSchema;
use storage_queue::QueueClient;

use super::template::{RunContext, Template};

pub fn build_analysis_config(
    args: &clap::ArgMatches,
    input_queue: Option<QueueClient>,
    common: CommonConfig,
    event_sender: Option<Sender<UiEvent>>,
) -> Result<Config> {
    let target_exe = get_cmd_exe(CmdType::Target, args)?.into();
    let target_options = get_cmd_arg(CmdType::Target, args);

    let analyzer_exe = args
        .get_one::<String>(ANALYZER_EXE)
        .cloned()
        .ok_or_else(|| format_err!("expected {ANALYZER_EXE}"))?;

    let analyzer_options = args
        .get_many::<String>(ANALYZER_OPTIONS)
        .unwrap_or_default()
        .map(|x| x.to_string())
        .collect();

    let analyzer_env = get_hash_map(args, ANALYZER_ENV)?;
    let analysis = get_synced_dir(ANALYSIS_DIR, common.job_id, common.task_id, args)?
        .monitor_count(&event_sender)?;
    let tools = get_synced_dir(TOOLS_DIR, common.job_id, common.task_id, args)?;
    let crashes = if input_queue.is_none() {
        get_synced_dir(CRASHES_DIR, common.job_id, common.task_id, args)
            .ok()
            .monitor_count(&event_sender)?
    } else {
        None
    };
    let reports = get_synced_dir(REPORTS_DIR, common.job_id, common.task_id, args)
        .ok()
        .monitor_count(&event_sender)?;
    let no_repro = get_synced_dir(NO_REPRO_DIR, common.job_id, common.task_id, args)
        .ok()
        .monitor_count(&event_sender)?;
    let unique_reports = get_synced_dir(UNIQUE_REPORTS_DIR, common.job_id, common.task_id, args)
        .ok()
        .monitor_count(&event_sender)?;

    let config = Config {
        analyzer_exe,
        analyzer_options,
        analyzer_env,
        target_exe,
        target_options,
        input_queue,
        crashes,
        analysis,
        tools: Some(tools),
        reports,
        unique_reports,
        no_repro,
        common,
    };

    Ok(config)
}

pub async fn run(args: &clap::ArgMatches, event_sender: Option<Sender<UiEvent>>) -> Result<()> {
    let context = build_local_context(args, true, event_sender.clone()).await?;
    let config = build_analysis_config(args, None, context.common_config.clone(), event_sender)?;
    run_analysis(config).await
}

pub fn build_shared_args(required_task: bool) -> Vec<Arg> {
    vec![
        Arg::new(TARGET_EXE).long(TARGET_EXE).required(true),
        Arg::new(TARGET_ENV)
            .long(TARGET_ENV)
            .requires(TARGET_EXE)
            .num_args(0..),
        Arg::new(TARGET_OPTIONS)
            .long(TARGET_OPTIONS)
            .default_value("{input}")
            .value_delimiter(' ')
            .help("Use a quoted string with space separation to denote multiple arguments"),
        Arg::new(CRASHES_DIR)
            .long(CRASHES_DIR)
            .value_parser(value_parser!(PathBuf)),
        Arg::new(ANALYZER_OPTIONS)
            .long(ANALYZER_OPTIONS)
            .requires(ANALYZER_EXE)
            .value_delimiter(' ')
            .help("Use a quoted string with space separation to denote multiple arguments"),
        Arg::new(ANALYZER_ENV)
            .long(ANALYZER_ENV)
            .requires(ANALYZER_EXE)
            .num_args(0..),
        Arg::new(TOOLS_DIR)
            .long(TOOLS_DIR)
            .value_parser(value_parser!(PathBuf)),
        Arg::new(ANALYZER_EXE)
            .long(ANALYZER_EXE)
            .requires(ANALYSIS_DIR)
            .requires(CRASHES_DIR)
            .required(required_task),
        Arg::new(ANALYSIS_DIR)
            .long(ANALYSIS_DIR)
            .requires(ANALYZER_EXE)
            .requires(CRASHES_DIR)
            .required(required_task),
    ]
}

pub fn args(name: &'static str) -> Command {
    Command::new(name)
        .about("execute a local-only generic analysis")
        .args(&build_shared_args(true))
}

#[derive(Debug, Serialize, Deserialize, Clone, JsonSchema)]
pub struct Analysis {
    analyzer_exe: String,
    analyzer_options: Vec<String>,
    analyzer_env: HashMap<String, String>,
    target_exe: PathBuf,
    target_options: Vec<String>,
    input_queue: Option<PathBuf>,
    crashes: Option<PathBuf>,
    analysis: PathBuf,
    tools: Option<PathBuf>,
    reports: Option<PathBuf>,
    unique_reports: Option<PathBuf>,
    no_repro: Option<PathBuf>,
}

#[async_trait]
impl Template for Analysis {
    async fn run(&self, context: &RunContext) -> Result<()> {
        let input_q = if let Some(w) = &self.input_queue {
            Some(context.monitor_dir(w).await?)
        } else {
            None
        };

        let analysis_config = crate::tasks::analysis::generic::Config {
            analyzer_exe: self.analyzer_exe.clone(),
            analyzer_options: self.analyzer_options.clone(),
            analyzer_env: self.analyzer_env.clone(),

            target_exe: self.target_exe.clone(),
            target_options: self.target_options.clone(),
            input_queue: input_q,
            crashes: self
                .crashes
                .as_ref()
                .and_then(|path| context.to_monitored_sync_dir("crashes", path).ok()),

            analysis: context.to_monitored_sync_dir("analysis", self.analysis.clone())?,
            tools: self
                .tools
                .as_ref()
                .and_then(|path| context.to_monitored_sync_dir("tools", path).ok()),

            reports: self
                .reports
                .as_ref()
                .and_then(|path| context.to_monitored_sync_dir("reports", path).ok()),
            unique_reports: self
                .unique_reports
                .as_ref()
                .and_then(|path| context.to_monitored_sync_dir("unique_reports", path).ok()),
            no_repro: self
                .no_repro
                .as_ref()
                .and_then(|path| context.to_monitored_sync_dir("no_repro", path).ok()),

            common: CommonConfig {
                task_id: uuid::Uuid::new_v4(),
                ..context.common.clone()
            },
        };

        context
            .spawn(async move { crate::tasks::analysis::generic::run(analysis_config).await })
            .await;

        Ok(())
    }
}
