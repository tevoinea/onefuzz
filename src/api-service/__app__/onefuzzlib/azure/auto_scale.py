#!/usr/bin/env python
#
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

import logging
import uuid
from datetime import timedelta
from typing import Any, Dict, Optional, Union
from uuid import UUID

from azure.core.exceptions import ResourceNotFoundError
from azure.mgmt.monitor.models import (
    AutoscaleProfile,
    AutoscaleSettingResource,
    ComparisonOperationType,
    MetricStatisticType,
    MetricTrigger,
    ScaleAction,
    ScaleCapacity,
    ScaleDirection,
    ScaleRule,
    ScaleType,
    TimeAggregationType,
)
from msrestazure.azure_exceptions import CloudError
from onefuzztypes.enums import ErrorCode
from onefuzztypes.models import Error
from onefuzztypes.primitives import Region

from .creds import (
    get_base_region,
    get_base_resource_group,
    get_subscription,
    retry_on_auth_failure,
)
from .monitor import get_monitor_client


@retry_on_auth_failure()
def add_auto_scale_to_vmss(
    vmss: UUID, auto_scale_profile: AutoscaleProfile
) -> Optional[Error]:
    logging.error("Checking scaleset %s for existing auto scale resources" % vmss)
    client = get_monitor_client()
    resource_group = get_base_resource_group()

    auto_scale_resource_id = None

    try:
        auto_scale_collections = client.autoscale_settings.list_by_resource_group(
            resource_group
        )
        for auto_scale in auto_scale_collections:
            if str(auto_scale.target_resource_uri).endswith(str(vmss)):
                auto_scale_resource_id = auto_scale.id
                break
    except (ResourceNotFoundError, CloudError):
        return Error(
            code=ErrorCode.INVALID_CONFIGURATION,
            errors=[
                "Failed to check if scaleset %s already has an autoscale resource"
                % vmss
            ],
        )

    if auto_scale_resource_id is not None:
        logging.warning("Scaleset %s already has auto scale resource" % vmss)
        return None

    resource_creation = create_auto_scale_resource_for(
        vmss, get_base_region(), auto_scale_profile
    )
    if isinstance(resource_creation, Error):
        return resource_creation
    return None


def create_auto_scale_resource_for(
    resource_id: UUID, location: Region, profile: AutoscaleProfile
) -> Union[AutoscaleSettingResource, Error]:
    logging.error("Creating auto scale resource for: %s" % resource_id)
    client = get_monitor_client()
    resource_group = get_base_resource_group()
    subscription = get_subscription()

    scaleset_uri = (
        "/subscriptions/%s/resourceGroups/%s/providers/Microsoft.Compute/virtualMachineScaleSets/%s"  # noqa: E501
        % (subscription, resource_group, resource_id)
    )

    params: Dict[str, Any] = {
        "location": location,
        "profiles": [profile],
        "target_resource_uri": scaleset_uri,
        "enabled": True,
    }

    try:
        auto_scale_resource = client.autoscale_settings.create_or_update(
            resource_group, str(uuid.uuid4()), params
        )
        logging.error(
            "Successfully created auto scale resource %s for %s"
            % (auto_scale_resource.id, resource_id)
        )
        return auto_scale_resource
    except (ResourceNotFoundError, CloudError):
        return Error(
            code=ErrorCode.UNABLE_TO_CREATE,
            errors=[
                "unable to create auto scale resource for resource: %s with profile: %s"
                % (resource_id, profile)
            ],
        )


def create_auto_scale_profile(min: int, max: int, queue_uri: str) -> AutoscaleProfile:
    return AutoscaleProfile(
        name=str(uuid.uuid4()),
        capacity=ScaleCapacity(minimum=min, maximum=max, default=max),
        rules=[
            ScaleRule(
                metric_trigger=MetricTrigger(
                    metric_name="ApproximateMessageCount",
                    metric_resource_uri=queue_uri,
                    # Check every minute
                    time_grain=timedelta(minutes=1),
                    # The average amount of messages there are in the pool queue
                    time_aggregation=TimeAggregationType.AVERAGE,
                    statistic=MetricStatisticType.COUNT,
                    # Over the past 10 minutes
                    time_window=timedelta(minutes=10),
                    # When there's more than 1 message in the pool queue
                    operator=ComparisonOperationType.GREATER_THAN,
                    threshold=1,
                ),
                scale_action=ScaleAction(
                    direction=ScaleDirection.INCREASE,
                    type=ScaleType.CHANGE_COUNT,
                    value=1,
                    cooldown=timedelta(minutes=5),
                ),
            ),
            # Scale in
            ScaleRule(
                # Scale in if no work in the past 10 mins
                metric_trigger=MetricTrigger(
                    metric_name="ApproximateMessageCount",
                    metric_resource_uri=queue_uri,
                    # Check every 10 minutes
                    time_grain=timedelta(minutes=10),
                    # The average amount of messages there are in the pool queue
                    time_aggregation=TimeAggregationType.AVERAGE,
                    statistic=MetricStatisticType.SUM,
                    # Over the past 10 minutes
                    time_window=timedelta(minutes=10),
                    # When there's no messages in the pool queue
                    operator=ComparisonOperationType.EQUALS,
                    threshold=0,
                ),
                scale_action=ScaleAction(
                    direction=ScaleDirection.DECREASE,
                    type=ScaleType.CHANGE_COUNT,
                    value=1,
                    cooldown=timedelta(minutes=5),
                ),
            )
        ],
    )
